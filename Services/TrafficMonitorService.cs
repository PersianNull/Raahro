using System.Collections.Concurrent;
using Raahro.Models;

namespace Raahro.Services;

public class TrafficMonitorService : ITrafficMonitorService
{
    private class Counter
    {
        public long Received;
        public long Sent;
        public long LastReceived;
        public long LastSent;
        public DateTime LastSample = DateTime.UtcNow;
        public double DownloadMbps;
        public double UploadMbps;
    }

    private readonly ConcurrentDictionary<string, Counter> _map = new(StringComparer.OrdinalIgnoreCase);

    public ClientTrafficStats GetStats(string clientKey)
    {
        if (!_map.TryGetValue(clientKey, out var c))
        {
            return new ClientTrafficStats { ClientId = clientKey };
        }

        var now = DateTime.UtcNow;
        var dt = (now - c.LastSample).TotalSeconds;
        if (dt >= 1)
        {
            var dRecv = c.Received - c.LastReceived;
            var dSent = c.Sent - c.LastSent;
            c.DownloadMbps = dt > 0 ? dRecv * 8.0 / dt / 1_000_000.0 : 0;
            c.UploadMbps = dt > 0 ? dSent * 8.0 / dt / 1_000_000.0 : 0;
            c.LastReceived = c.Received;
            c.LastSent = c.Sent;
            c.LastSample = now;
        }

        return new ClientTrafficStats
        {
            ClientId = clientKey,
            BytesReceived = c.Received,
            BytesSent = c.Sent,
            DownloadMbps = c.DownloadMbps,
            UploadMbps = c.UploadMbps,
            UpdatedAt = now
        };
    }

    public void AddBytes(string clientKey, long received, long sent)
    {
        var c = _map.GetOrAdd(clientKey, _ => new Counter());
        Interlocked.Add(ref c.Received, received);
        Interlocked.Add(ref c.Sent, sent);
    }

    public void Reset() => _map.Clear();
}