using Raahro.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace Raahro.Services
{
    public interface IAdapterService
    {
        IReadOnlyList<AdapterInfo> GetAllAdapters();
        AdapterInfo? FindByKeywords(params string[] keywords);
        AdapterInfo? FindHotspotAdapter();
        AdapterInfo? FindTunAdapter();
    }

    
}
