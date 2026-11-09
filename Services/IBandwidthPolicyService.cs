using Raahro.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Services
{
    public interface IBandwidthPolicyService
    {
        BandwidthLimit GetLimit(string macAddress);
        void SetLimit(string macAddress, BandwidthLimit limit, string? displayName = null);
        void Remove(string macAddress);
        IReadOnlyList<ClientPolicy> GetAll();
        Task LoadAsync();
        Task SaveAsync();
    }
}
