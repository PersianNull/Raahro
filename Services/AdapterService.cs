using Raahro.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace Raahro.Services
{
    public class AdapterService : IAdapterService
    {
        public IReadOnlyList<AdapterInfo> GetAllAdapters()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(n => new AdapterInfo
                {
                    Name = n.Name,
                    Description = n.Description,
                    IsUp = n.OperationalStatus == OperationalStatus.Up
                })
                .ToList();
        }

        public AdapterInfo? FindByKeywords(params string[] keywords)
        {
            return GetAllAdapters()
                .FirstOrDefault(a =>
                    keywords.Any(k =>
                        a.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        public AdapterInfo? FindHotspotAdapter()
        {
            var adapters = GetAllAdapters()
                .Where(a => a.IsUp)
                .ToList();

            static bool IsExcluded(AdapterInfo a)
            {
                var blob = $"{a.Name} {a.Description}".ToLowerInvariant();
                return blob.Contains("tap")
                       || blob.Contains("tun")
                       || blob.Contains("wintun")
                       || blob.Contains("sing-box")
                       || blob.Contains("singbox")
                       || blob.Contains("proxyshare")
                       || blob.Contains("vmware")
                       || blob.Contains("virtualbox")
                       || blob.Contains("hyper-v")
                       || blob.Contains("vethernet")
                       || blob.Contains("loopback")
                       || blob.Contains("vpn");
            }

            var candidates = adapters.Where(a => !IsExcluded(a)).ToList();

            foreach (var a in candidates)
            {
                if (HasPrefix(a.Name, "192.168.137."))
                    return a;
            }

            var direct = candidates.FirstOrDefault(a =>
                a.Description.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct;

            var hosted = candidates.FirstOrDefault(a =>
                a.Description.Contains("Hosted Network", StringComparison.OrdinalIgnoreCase));
            if (hosted != null) return hosted;

            return candidates.FirstOrDefault(a =>
                a.Name.StartsWith("Local Area Connection*", StringComparison.OrdinalIgnoreCase));
        }

        public AdapterInfo? FindTunAdapter()
        {
            var byName = FindByKeywords("ProxyShareTun", "singbox", "sing-box", "Wintun");
            if (byName != null && byName.IsUp) return byName;

            return GetAllAdapters()
                .Where(a => a.IsUp)
                .FirstOrDefault(a =>
                {
                    var blob = $"{a.Name} {a.Description}";
                    return blob.Contains("ProxyShareTun", StringComparison.OrdinalIgnoreCase)
                           || blob.Contains("sing-box", StringComparison.OrdinalIgnoreCase)
                           || blob.Contains("singbox", StringComparison.OrdinalIgnoreCase)
                           || blob.Contains("Wintun", StringComparison.OrdinalIgnoreCase);
                });
        }

        private static bool HasPrefix(string adapterName, string ipPrefix)
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Name == adapterName);
                if (nic == null) return false;

                return nic.GetIPProperties().UnicastAddresses.Any(u =>
                    u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    u.Address.ToString().StartsWith(ipPrefix, StringComparison.Ordinal));
            }
            catch
            {
                return false;
            }
        }
    }
}
