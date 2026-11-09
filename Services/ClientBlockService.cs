using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Raahro.Services;

public class ClientBlockService : IClientBlockService
{
    private readonly ConcurrentDictionary<string, string> _blockedMacs =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsBlocked(string macAddress) => _blockedMacs.ContainsKey(NormalizeMac(macAddress));

    public string? GetBlockedIp(string macAddress) =>
        _blockedMacs.TryGetValue(NormalizeMac(macAddress), out var ip) ? ip : null;

    public async Task<bool> BlockAsync(string macAddress, string? ipAddress, Action<string>? log = null)
    {
        var macKey = NormalizeMac(macAddress);
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            ipAddress = FindIpByMac(macKey);
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                log?.Invoke($"⛔ Block: IP برای {macAddress} پیدا نشد.");
                return false;
            }
        }

        if (_blockedMacs.TryGetValue(macKey, out var existingIp) && existingIp == ipAddress)
            return true; 

        try
        {
            var ruleBase = "Raahro_Block_" + macKey.Replace("-", "").Replace(":", "");

            await RunPowerShellAsync(
                $"Remove-NetFirewallRule -DisplayName '{ruleBase}_in' -ErrorAction SilentlyContinue; " +
                $"Remove-NetFirewallRule -DisplayName '{ruleBase}_out' -ErrorAction SilentlyContinue",
                log);

            await RunPowerShellAsync(
                $"New-NetFirewallRule -DisplayName '{ruleBase}_in' -Direction Inbound " +
                $"-RemoteAddress {ipAddress} -Action Block -Profile Any -ErrorAction Stop",
                log);

            await RunPowerShellAsync(
                $"New-NetFirewallRule -DisplayName '{ruleBase}_out' -Direction Outbound " +
                $"-RemoteAddress {ipAddress} -Action Block -Profile Any -ErrorAction Stop",
                log);

            _blockedMacs[macKey] = ipAddress;
            log?.Invoke($"✅ Blocked: {macAddress} ({ipAddress})");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("Block error: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> UnblockAsync(string macAddress, Action<string>? log = null)
    {
        var macKey = NormalizeMac(macAddress);
        try
        {
            var ruleBase = "Raahro_Block_" + macKey.Replace("-", "").Replace(":", "");
            await RunPowerShellAsync(
                $"Remove-NetFirewallRule -DisplayName '{ruleBase}_in' -ErrorAction SilentlyContinue; " +
                $"Remove-NetFirewallRule -DisplayName '{ruleBase}_out' -ErrorAction SilentlyContinue",
                log);

            _blockedMacs.TryRemove(macKey, out _);
            log?.Invoke($"✅ Unblocked: {macAddress}");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("Unblock error: " + ex.Message);
            return false;
        }
    }

    public async Task ClearAllAsync(Action<string>? log = null)
    {
        try
        {
            await RunPowerShellAsync(
                "Get-NetFirewallRule -DisplayName 'Raahro_Block_*' -ErrorAction SilentlyContinue | " +
                "Remove-NetFirewallRule -ErrorAction SilentlyContinue",
                log);
            _blockedMacs.Clear();
            log?.Invoke("All block rules cleared.");
        }
        catch (Exception ex)
        {
            log?.Invoke("ClearAll error: " + ex.Message);
        }
    }

    private static string NormalizeMac(string mac) =>
        mac.Trim().Replace(':', '-').ToUpperInvariant();

    private static string? FindIpByMac(string macAddress)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = "-a",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            var normalized = macAddress.Replace(':', '-').ToUpperInvariant();
            var regex = new Regex(@"(\d+\.\d+\.\d+\.\d+)\s+([0-9a-fA-F\-]{17})");
            foreach (Match m in regex.Matches(output))
            {
                if (m.Groups[2].Value.Replace(':', '-').ToUpperInvariant() == normalized)
                    return m.Groups[1].Value;
            }
        }
        catch { }
        return null;
    }

    private static async Task<string> RunPowerShellAsync(string script, Action<string>? log)
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
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output + err;
    }
}