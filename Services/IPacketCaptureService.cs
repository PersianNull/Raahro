using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Services
{
    public interface IPacketCaptureService
    {
        bool IsRunning { get; }
        void Start(string adapterName, ITrafficMonitorService traffic, Action<string>? log = null);
        void Stop();
    }
}
