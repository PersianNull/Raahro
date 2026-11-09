using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Services
{
    public interface IIcsService
    {
        bool EnableSharing(string publicAdapterName, string privateAdapterName, Action<string>? log = null);
        void DisableAllSharing(Action<string>? log = null);
    }
}
