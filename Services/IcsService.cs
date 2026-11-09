using NETCONLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Services
{
    public class IcsService : IIcsService
    {
        public bool EnableSharing(string publicAdapterName, string privateAdapterName, Action<string>? log = null)
        {
            try
            {
                var mgr = new NetSharingManager();
                if (!mgr.SharingInstalled)
                {
                    log?.Invoke("ICS is not supported on this system.");
                    return false;
                }

                INetConnection? publicConn = null;
                INetConnection? privateConn = null;

                foreach (INetConnection conn in mgr.EnumEveryConnection)
                {
                    var props = mgr.NetConnectionProps[conn];
                    if (props.Name.Equals(publicAdapterName, StringComparison.OrdinalIgnoreCase))
                        publicConn = conn;
                    if (props.Name.Equals(privateAdapterName, StringComparison.OrdinalIgnoreCase))
                        privateConn = conn;
                }

                if (publicConn is null || privateConn is null)
                {
                    log?.Invoke("Required adapters for ICS not found.");
                    return false;
                }

                // پاک‌سازی اشتراک‌های قبلی
                foreach (INetConnection conn in mgr.EnumEveryConnection)
                {
                    var cfg = mgr.INetSharingConfigurationForINetConnection[conn];
                    if (cfg.SharingEnabled) cfg.DisableSharing();
                }

                var publicCfg = mgr.INetSharingConfigurationForINetConnection[publicConn];
                var privateCfg = mgr.INetSharingConfigurationForINetConnection[privateConn];

                publicCfg.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PUBLIC);
                privateCfg.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PRIVATE);

                log?.Invoke("ICS enabled successfully.");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("ICS Exception: " + ex.Message);
                return false;
            }
        }

        public void DisableAllSharing(Action<string>? log = null)
        {
            try
            {
                var mgr = new NetSharingManager();
                foreach (INetConnection conn in mgr.EnumEveryConnection)
                {
                    var cfg = mgr.INetSharingConfigurationForINetConnection[conn];
                    if (cfg.SharingEnabled) cfg.DisableSharing();
                }
                log?.Invoke("All ICS sharings disabled.");
            }
            catch (Exception ex)
            {
                log?.Invoke("Disable ICS error: " + ex.Message);
            }
        }
    }
}
