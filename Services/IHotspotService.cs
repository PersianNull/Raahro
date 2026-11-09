using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Raahro.Services
{
    public interface IHotspotService
    {
        Task<bool> StartAsync(Action<string>? log = null);
        Task StopAsync(Action<string>? log = null);
    }

    
}
