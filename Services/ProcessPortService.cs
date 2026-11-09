using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Raahro.Services;

public static class ProcessPortService
{
    public static string? FindListenerProcessName(int port)
    {
        try
        {
            foreach (var p in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                if (p.Port != port) continue;
                // فقط listenerها؛ PID از netstat پایدارتر است
            }
        }
        catch { /* ادامه با netstat */ }

        return FindByNetstat(port);
    }

    private static string? FindByNetstat(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano -p tcp | findstr :{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            // TCP    127.0.0.1:10808    0.0.0.0:0    LISTENING    1234
            var pids = new HashSet<int>();
            foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
                if (!line.Contains($":{port}")) continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                if (int.TryParse(parts[^1], out var pid) && pid > 0)
                    pids.Add(pid);
            }

            foreach (var pid in pids)
            {
                try
                {
                    var name = Process.GetProcessById(pid).ProcessName;
                    if (!string.IsNullOrWhiteSpace(name))
                        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            ? name
                            : name + ".exe";
                }
                catch { }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}