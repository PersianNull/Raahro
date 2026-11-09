using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Raahro.Helpers
{
    public static class PathHelper
    {
        public static string? FindSingBoxExecutable()
        {
            var candidates = new[]
            {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sing-box.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "sing-box.exe")
        };

            return candidates.FirstOrDefault(File.Exists);
        }

        public static string GetTempConfigPath() =>
            Path.Combine(Path.GetTempPath(), $"sb-hotspot-{Guid.NewGuid():N}.json");
    }
}
