namespace Raahro.Models;

public class BandwidthLimit
{
    public int DownloadKbps { get; set; }

    public int UploadKbps { get; set; }

    public bool IsBlocked { get; set; }
}