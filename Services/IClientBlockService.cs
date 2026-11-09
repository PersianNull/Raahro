using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Services
{
    public interface IClientBlockService
    {
        Task<bool> BlockAsync(string macAddress, string? ipAddress, Action<string>? log = null);
        Task<bool> UnblockAsync(string macAddress, Action<string>? log = null);
        Task ClearAllAsync(Action<string>? log = null);
        bool IsBlocked(string macAddress);
        string? GetBlockedIp(string macAddress);
    }
}
