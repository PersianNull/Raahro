using Raahro.Models;
using Raahro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace Raahro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISingBoxService _singBox;
    private readonly IHotspotService _hotspot;
    private readonly IIcsService _ics;
    private readonly IAdapterService _adapters;
    private readonly IClientDiscoveryService _clientDiscovery;
    private readonly IBandwidthPolicyService _policies;
    private readonly ITrafficMonitorService _traffic;
    private readonly IPacketCaptureService _packetCapture;
    private readonly IClientBlockService _blockService;

    private CancellationTokenSource? _clientsCts;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _blockBusy
        = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<ConnectedClient> ConnectedClients { get; } = new();

    [ObservableProperty] private int _connectedCount;
    [ObservableProperty] private ConnectedClient? _selectedClient;

    [ObservableProperty] private string _portText = "10808";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _detailText = "Enter the proxy port and press Start.";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isBusy;
    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public ObservableCollection<string> Logs { get; } = new();

    public MainViewModel(
        ISingBoxService singBox,
        IHotspotService hotspot,
        IIcsService ics,
        IAdapterService adapters,
        IClientDiscoveryService clientDiscovery,
        IBandwidthPolicyService policies,
        ITrafficMonitorService traffic,
        IPacketCaptureService packetCapture,
        IClientBlockService blockService)
    {
        _singBox = singBox;
        _hotspot = hotspot;
        _ics = ics;
        _adapters = adapters;
        _clientDiscovery = clientDiscovery;
        _policies = policies;
        _traffic = traffic;
        _packetCapture = packetCapture;
        _blockService = blockService;
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!int.TryParse(PortText.Trim(), out int port) || port is < 1 or > 65535)
        {
            MessageBox.Show("Please enter a valid port (1-65535).", "Invalid Port");
            return;
        }

        IsBusy = true;
        try
        {
            AddLog("Starting process...");

            // Disable any leftover IP forwarding from previous runs
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Get-NetIPInterface | Where-Object {$_.Forwarding -eq 'Enabled'} | Set-NetIPInterface -Forwarding Disabled\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit(4000);
                AddLog("IP forwarding disabled (best effort).");
            }
            catch (Exception ex)
            {
                AddLog("Forwarding cleanup: " + ex.Message);
            }

            await _singBox.StartAsync(port, AddLog);

            AdapterInfo? tun = null;
            for (var i = 1; i <= 8; i++)
            {
                await Task.Delay(1000);
                tun = _adapters.FindTunAdapter()
                      ?? _adapters.FindByKeywords("ProxyShareTun", "sing-box", "singbox", "Wintun");
                if (tun is not null && tun.IsUp)
                {
                    AddLog($"TUN Adapter: {tun.Name}");
                    break;
                }
                AddLog($"Waiting for TUN... ({i}/8)");
            }

            if (tun is null)
            {
                AddLog("TUN adapter not found.");
                await SafeStopAsync();
                return;
            }

            IsRunning = true;
            StatusText = "Running";
            DetailText = $"TUN active | SOCKS:{port}";

            var ok = await _hotspot.StartAsync(AddLog);
            if (!ok)
                AddLog("Automatic hotspot startup failed; enable it manually if needed.");

            await Task.Delay(2500);
            var hotspot = _adapters.FindHotspotAdapter();

            if (hotspot is null)
            {
                AddLog("Hotspot adapter not found — ICS cannot be applied.");
                return;
            }

            AddLog($"Hotspot Adapter: {hotspot.Name}  (TUN: {tun.Name})");

            bool icsOk = false;
            try
            {
                icsOk = _ics.EnableSharing(tun.Name, hotspot.Name, AddLog);
            }
            catch (Exception ex)
            {
                AddLog("ICS exception: " + ex.Message);
            }

            if (icsOk)
            {
                DetailText = $"TUN:{tun.Name} | Hotspot:{hotspot.Name} | ICS enabled | SOCKS:{port}";
                AddLog("✅ ICS enabled successfully.");

                _packetCapture.Start(hotspot.Name, _traffic, AddLog);
                StartClientPolling();
            }
            else
            {
                AddLog("❌ ICS failed. Hotspot clients will not have internet access.");
            }
        }
        catch (Exception ex)
        {
            AddLog("Error: " + ex.Message);
            MessageBox.Show(ex.Message, "Error");
            await SafeStopAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleBlockAsync(ConnectedClient? client)
    {
        if (client == null || string.IsNullOrWhiteSpace(client.MacAddress)) return;
        if (!_blockBusy.TryAdd(client.MacAddress, 0)) return; // already processing

        try
        {
            var wasBlocked = client.IsBlocked;
            client.IsBlocked = !wasBlocked; // optimistic UI

            bool ok;
            if (wasBlocked)
                ok = await _blockService.UnblockAsync(client.MacAddress, AddLog);
            else
                ok = await _blockService.BlockAsync(client.MacAddress, client.IpAddress, AddLog);

            if (!ok)
            {
                client.IsBlocked = wasBlocked; // revert
                AddLog($"⚠ Failed to toggle block state for {client.MacAddress}.");
            }
        }
        finally
        {
            _blockBusy.TryRemove(client.MacAddress, out _);
        }
    }

    private void StartClientPolling()
    {
        _clientsCts?.Cancel();
        _clientsCts = new CancellationTokenSource();
        var token = _clientsCts.Token;

        // --- Loop 1: discover new clients ---
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var discovered = await _clientDiscovery.GetConnectedClientsAsync();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        var previousSelectedMac = SelectedClient?.MacAddress;

                        var existingByMac = ConnectedClients
                            .Where(x => !string.IsNullOrWhiteSpace(x.MacAddress))
                            .ToDictionary(x => x.MacAddress, StringComparer.OrdinalIgnoreCase);

                        var newMacs = discovered
                            .Where(d => !string.IsNullOrWhiteSpace(d.MacAddress))
                            .Select(d => d.MacAddress)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        for (int i = ConnectedClients.Count - 1; i >= 0; i--)
                        {
                            var c = ConnectedClients[i];
                            if (!string.IsNullOrWhiteSpace(c.MacAddress) && !newMacs.Contains(c.MacAddress))
                                ConnectedClients.RemoveAt(i);
                        }

                        foreach (var d in discovered)
                        {
                            if (string.IsNullOrWhiteSpace(d.MacAddress)) continue;
                            if (existingByMac.ContainsKey(d.MacAddress)) continue;
                            ConnectedClients.Add(d);
                        }

                        foreach (var c in ConnectedClients.ToList())
                        {
                            if (_blockService.IsBlocked(c.MacAddress))
                            {
                                c.IsBlocked = true;
                                var blockedIp = _blockService.GetBlockedIp(c.MacAddress);
                                if (!string.IsNullOrWhiteSpace(c.IpAddress) && c.IpAddress != blockedIp)
                                {
                                    _ = Task.Run(() => _blockService.BlockAsync(c.MacAddress, c.IpAddress, AddLog));
                                }
                            }
                        }

                        ConnectedCount = ConnectedClients.Count;

                        if (!string.IsNullOrWhiteSpace(previousSelectedMac))
                            SelectedClient = ConnectedClients
                                .FirstOrDefault(x => x.MacAddress == previousSelectedMac);
                    });
                }
                catch (Exception ex) { AddLog("Client discovery: " + ex.Message); }

                try { await Task.Delay(3000, token); } catch (TaskCanceledException) { break; }
            }
        }, token);

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        foreach (var c in ConnectedClients)
                        {
                            var key = !string.IsNullOrWhiteSpace(c.MacAddress) ? c.MacAddress : c.IpAddress;
                            var stats = _traffic.GetStats(key);
                            c.BytesReceived = stats.BytesReceived;
                            c.BytesSent = stats.BytesSent;
                            c.DownloadMbps = stats.DownloadMbps;
                            c.UploadMbps = stats.UploadMbps;
                        }
                    });
                }
                catch { }

                try { await Task.Delay(500, token); } catch (TaskCanceledException) { break; }
            }
        }, token);
    }

    private void StopClientPolling()
    {
        _clientsCts?.Cancel();
        _clientsCts = null;
        _traffic.Reset();

        Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedClients.Clear();
            ConnectedCount = 0;
            SelectedClient = null;
        });
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        IsBusy = true;
        try
        {
            await SafeStopAsync();
            StatusText = "Stopped";
            DetailText = "Everything has been restored to its previous state.";
            IsRunning = false;
        }
        finally
        {
            IsBusy = false;
        }
        ClearAllState();
    }
    private void ClearAllState()
    {
        _traffic.Reset();

        Application.Current.Dispatcher.Invoke(() =>
        {
            Logs.Clear();
            LogText = string.Empty;
        });

        StatusText = "Ready";
        DetailText = "Enter the proxy port and press Start.";
        IsRunning = false;

    }
    [RelayCommand]
    private void CopyLogs()
    {
        if (string.IsNullOrWhiteSpace(LogText)) return;
        Clipboard.SetText(LogText);
    }

    private async Task SafeStopAsync()
    {
        AddLog("Initiating emergency shutdown...");

        try { StopClientPolling(); }
        catch { }

        try { _packetCapture.Stop(); }
        catch (Exception ex) { AddLog("PacketCapture stop: " + ex.Message); }

        // 1) sing-box
        try { await _singBox.StopAsync(); }
        catch (Exception ex) { AddLog("Stop sing-box: " + ex.Message); }

        // 2) precautionary taskkill
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM sing-box.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi)?.WaitForExit(3000);
            AddLog("taskkill sing-box executed.");
        }
        catch (Exception ex) { AddLog("taskkill: " + ex.Message); }

        // 3) ICS
        try { _ics.DisableAllSharing(AddLog); }
        catch (Exception ex) { AddLog("ICS stop: " + ex.Message); }

        // 3.5) Cleanup firewall block rules
        try { await _blockService.ClearAllAsync(AddLog); }
        catch (Exception ex) { AddLog("Block cleanup: " + ex.Message); }

        // 4) Hotspot
        try { await _hotspot.StopAsync(AddLog); }
        catch (Exception ex) { AddLog("Hotspot stop: " + ex.Message); }

        AddLog("Shutdown complete.");
    }

    private void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        Application.Current.Dispatcher.Invoke(() =>
        {
            Logs.Add(line);
            while (Logs.Count > 500) Logs.RemoveAt(0);
            LogText = string.Join(Environment.NewLine, Logs);
        });
    }
}