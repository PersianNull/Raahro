using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Raahro.Helpers
{
    public static class AdminHelper
    {
        public static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
