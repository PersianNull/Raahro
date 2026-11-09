using Raahro.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Raahro.Services
{
    public class SingBoxService : ISingBoxService
    {
        private Process? _process;
        private string? _configPath;

        public bool IsRunning => _process is { HasExited: false };

        public async Task StartAsync(int socksPort, Action<string>? log = null)
        {
            if (IsRunning) return;

            var exe = PathHelper.FindSingBoxExecutable()
                ?? throw new FileNotFoundException("sing-box.exe not found.");

            var proxyProcessName = ProcessPortService.FindListenerProcessName(socksPort);
            if (string.IsNullOrWhiteSpace(proxyProcessName))
            {
                throw new InvalidOperationException(
    $"No process found in LISTENING state on port {socksPort}. Start SOCKS first.");
            }

            log?.Invoke($"SOCKS process on port {socksPort}: {proxyProcessName}");

            _configPath = PathHelper.GetTempConfigPath();

            await File.WriteAllTextAsync(
                _configPath,
                BuildConfig(socksPort, proxyProcessName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"run -c \"{_configPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe)!
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) log?.Invoke("[sing-box] " + e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) log?.Invoke("[sing-box] " + e.Data);
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await Task.Delay(2800);

            if (_process.HasExited)
                throw new InvalidOperationException("sing-box exited immediately.");
        }

        public async Task StopAsync()
        {
            try
            {
                if (_process is { HasExited: false })
                {
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        try { _process.Kill(); } catch { }
                    }

                    // بیشتر از 2 ثانیه صبر نکن
                    var exited = _process.WaitForExit(2000);
                    if (!exited)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = "/F /IM sing-box.exe",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            Process.Start(psi)?.WaitForExit(2000);
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                try { _process?.Dispose(); } catch { }
                _process = null;

                if (_configPath != null && File.Exists(_configPath))
                {
                    try { File.Delete(_configPath); } catch { }
                }
                _configPath = null;
            }

            await Task.CompletedTask;
        }

        private static string BuildConfig(int port, string? proxyProcessName)
        {
            var processNames = new List<string> { "sing-box.exe" };
            if (!string.IsNullOrWhiteSpace(proxyProcessName))
                processNames.Add(proxyProcessName);

            processNames = processNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var config = new
            {
                log = new { level = "warn", timestamp = true },
                dns = new
                {
                    servers = new object[]
                    {
                new
                {
                    type = "udp",
                    tag = "google",
                    server = "8.8.8.8",
                    detour = "proxy"
                },
                new { type = "local", tag = "local" }
                    },
                    final = "google",
                    strategy = "ipv4_only"
                },
                inbounds = new[]
                {
            new
            {
                type = "tun",
                tag = "tun-in",
                interface_name = "ProxyShareTun",
                address = new[] { "172.19.0.1/30" },
                mtu = 1400,
                auto_route = true,
                strict_route = false,
                stack = "system",
                route_exclude_address = new[]
                {
                    "192.168.0.0/16",
                    "10.0.0.0/8",
                    "172.16.0.0/12",
                    "224.0.0.0/4"
                }
            }
        },
                outbounds = new object[]
                {
            new
            {
                type = "socks",
                tag = "proxy",
                server = "127.0.0.1",
                server_port = port,
                version = "5",
                domain_resolver = "local"
            },
            new
            {
                type = "direct",
                tag = "direct",
                domain_resolver = "local"
            }
                },
                route = new
                {
                    rules = new object[]
                    {
                new { action = "sniff" },
                new
                {
                    process_name = processNames.ToArray(),
                    outbound = "direct"
                },
                new
                {
                    ip_cidr = new[] { "127.0.0.0/8", "169.254.0.0/16" },
                    outbound = "direct"
                },
                new
                {
                    ip_is_private = true,
                    outbound = "direct"
                }
                    },
                    final = "proxy",
                    auto_detect_interface = true,
                    default_domain_resolver = "local"
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}
