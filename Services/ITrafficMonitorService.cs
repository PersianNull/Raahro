using Raahro.Models;

namespace Raahro.Services;

public interface ITrafficMonitorService
{
    ClientTrafficStats GetStats(string clientKey);
    void Reset();
    void AddBytes(string clientKey, long received, long sent);
}