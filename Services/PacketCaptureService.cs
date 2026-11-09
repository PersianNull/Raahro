using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Raahro.Services;

public class PacketCaptureService : IPacketCaptureService
{
    private ILiveDevice? _device;
    private ITrafficMonitorService? _traffic;
    private string? _localMac;

    public bool IsRunning => _device is not null;

    public void Start(string adapterName, ITrafficMonitorService traffic, Action<string>? log = null)
    {
        try
        {
            Stop();
            _traffic = traffic;

            // 1) NetworkInterface مدنظر رو پیدا کن تا MACش رو بدونیم
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase));

            if (nic is null)
            {
                log?.Invoke($"PacketCapture: NetworkInterface '{adapterName}' not found.");
                return;
            }

            var targetMacBytes = nic.GetPhysicalAddress().GetAddressBytes();
            if (targetMacBytes.Length == 0)
            {
                log?.Invoke("PacketCapture: adapter MAC is empty (unusual).");
                return;
            }

            _localMac = FormatMac(targetMacBytes);
            log?.Invoke($"PacketCapture: target MAC = {_localMac}");

            // 2) دستگاه Npcap متناظر رو با MAC پیدا کن (پایدارتر از نام)
            ILiveDevice? device = null;
            foreach (var dev in CaptureDeviceList.Instance)
            {
                try
                {
                    var addr = dev.MacAddress;
                    if (addr is null) continue;
                    if (addr.GetAddressBytes().SequenceEqual(targetMacBytes))
                    {
                        device = dev;
                        break;
                    }
                }
                catch { }
            }

            if (device is null)
            {
                log?.Invoke("PacketCapture: Npcap device not found for this adapter (is Npcap installed?).");
                return;
            }

            _device = device;

            // 3) هندلر دریافت پکت
            _device.OnPacketArrival += OnPacket;

            _device.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                ReadTimeout = 1000
            });
            _device.StartCapture();

            log?.Invoke($"PacketCapture: started on {_device.Name} ({_device.Description})");
        }
        catch (Exception ex)
        {
            log?.Invoke("PacketCapture error: " + ex.Message);
            Stop();
        }
    }

    public void Stop()
    {
        var dev = _device;
        _device = null;
        _traffic = null;

        if (dev is null) return;
        try
        {
            try { dev.OnPacketArrival -= OnPacket; } catch { }
            try { dev.StopCapture(); } catch { }
            try { dev.Close(); } catch { }
        }
        catch { }
    }

    private void OnPacket(object sender, PacketCapture e)
    {
        try
        {
            var raw = e.GetPacket();
            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data.ToArray());
            if (packet is not EthernetPacket eth) return;

            var srcMac = FormatMac(eth.SourceHardwareAddress.GetAddressBytes());
            var dstMac = FormatMac(eth.DestinationHardwareAddress.GetAddressBytes());
            var len = eth.TotalPacketLength;

            // پکت از کلاینت (فرستاده‌شده توسط کلاینت) → آپلود کلاینت
            if (!string.IsNullOrEmpty(srcMac)
                && srcMac != "FF-FF-FF-FF-FF-FF"
                && !string.Equals(srcMac, _localMac, StringComparison.OrdinalIgnoreCase))
            {
                _traffic?.AddBytes(srcMac, 0, len);
            }

            // پکت به کلاینت (مقصدش کلاینت هست) → دانلود کلاینت
            if (!string.IsNullOrEmpty(dstMac)
                && dstMac != "FF-FF-FF-FF-FF-FF"
                && !string.Equals(dstMac, _localMac, StringComparison.OrdinalIgnoreCase))
            {
                _traffic?.AddBytes(dstMac, len, 0);
            }
        }
        catch { /* packet parse error — ignore */ }
    }

    private static string FormatMac(byte[] bytes)
    {
        if (bytes.Length != 6) return "";
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }
}