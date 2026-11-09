using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Raahro.Models;

namespace Raahro.Services;

public class ClientDiscoveryService : IClientDiscoveryService
{
    public async Task<IReadOnlyList<ConnectedClient>> GetConnectedClientsAsync()
    {
        var result = new List<ConnectedClient>();

        var fromHotspot = await GetFromHotspotApiAsync();
        if (fromHotspot.Count > 0)
            return fromHotspot;

        var fromArp = await GetFromArpAsync();
        return fromArp;
    }

    private async Task<List<ConnectedClient>> GetFromHotspotApiAsync()
    {
        var list = new List<ConnectedClient>();

        try
        {
            const string script = """
$ErrorActionPreference = 'Stop'
try {
  $cp = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]::GetInternetConnectionProfile()
  if ($null -eq $cp) { return }
  $tm = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime]::CreateFromConnectionProfile($cp)
  if ($null -eq $tm) { return }
  $clients = $tm.GetTetheringClients()
  foreach ($c in $clients) {
    $mac = $c.MacAddress
    $host = $c.HostNames | Select-Object -First 1
    $hostName = if ($host) { $host.ToString() } else { 'Unknown' }
    Write-Output ($mac + '|' + $hostName)
  }
} catch {}
""";

            var output = await RunPowerShellAsync(script);
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) continue;

                list.Add(new ConnectedClient
                {
                    MacAddress = NormalizeMac(parts[0]),
                    HostName = parts.Length > 1 ? parts[1] : "Unknown",
                    ConnectedAt = DateTime.Now,
                    IsActive = true
                });
            }
        }
        catch
        {
        }

        return list;
    }

    private async Task<List<ConnectedClient>> GetFromArpAsync()
    {
        var list = new List<ConnectedClient>();

        try
        {
            var output = await RunPowerShellAsync("arp -a");
            var regex = new Regex(
                @"(\d+\.\d+\.\d+\.\d+)\s+([0-9a-fA-F\-]{17})\s+(\w+)",
                RegexOptions.Compiled);

            foreach (Match match in regex.Matches(output))
            {
                var ip = match.Groups[1].Value;
                var mac = NormalizeMac(match.Groups[2].Value);
                var type = match.Groups[3].Value;

                if (mac is "FF-FF-FF-FF-FF-FF" or "00-00-00-00-00-00") continue;
                if (type.Equals("static", StringComparison.OrdinalIgnoreCase) && ip.EndsWith(".255")) continue;

                var hotspotPrefix = GetHotspotSubnetPrefix();
                if (hotspotPrefix != null && !ip.StartsWith(hotspotPrefix, StringComparison.Ordinal))
                    continue;
                if (hotspotPrefix == null && !ip.StartsWith("192.168.137.") && !ip.StartsWith("192.168.173."))
                    continue;

                list.Add(new ConnectedClient
                {
                    IpAddress = ip,
                    MacAddress = mac,
                    HostName = "Unknown",
                    ConnectedAt = DateTime.Now,
                    IsActive = true
                });
            }
        }
        catch
        {
            
        }

        return list
            .GroupBy(c => c.MacAddress)
            .Select(g => g.First())
            .ToList();
    }

    private static string NormalizeMac(string mac)
    {
        mac = mac.Trim().Replace(':', '-').ToUpperInvariant();
        return mac;
    }
    private static string? GetHotspotSubnetPrefix()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.Name.StartsWith("Local Area Connection*", StringComparison.OrdinalIgnoreCase));
            if (nic == null) return null;

            var ip = nic.GetIPProperties().UnicastAddresses
                .FirstOrDefault(u => u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ip == null) return null;

            var parts = ip.Address.ToString().Split('.');
            return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.{parts[2]}." : null;
        }
        catch { return null; }
    }
    private static async Task<string> RunPowerShellAsync(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output;
    }
}