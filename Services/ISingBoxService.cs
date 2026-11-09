using Raahro.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Raahro.Services
{
    public interface ISingBoxService
    {
        bool IsRunning { get; }
        Task StartAsync(int socksPort, Action<string>? log = null);
        Task StopAsync();
    }

    
}
