using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Models
{
    public class ClientTrafficStats
    {
        public string ClientId { get; set; } = "";
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
