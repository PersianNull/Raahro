using CommunityToolkit.Mvvm.ComponentModel;

namespace Raahro.Models;

public partial class ConnectedClient : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MacAddress { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string HostName { get; set; } = "Unknown";
    public DateTime ConnectedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    private long _bytesReceived;
    public long BytesReceived
    {
        get => _bytesReceived;
        set
        {
            if (SetProperty(ref _bytesReceived, value))
            {
                OnPropertyChanged(nameof(TotalDownloadText));
                OnPropertyChanged(nameof(TotalUsageText));
            }
        }
    }

    private long _bytesSent;
    public long BytesSent
    {
        get => _bytesSent;
        set
        {
            if (SetProperty(ref _bytesSent, value))
            {
                OnPropertyChanged(nameof(TotalUploadText));
                OnPropertyChanged(nameof(TotalUsageText));
            }
        }
    }

    private double _downloadMbps;
    public double DownloadMbps
    {
        get => _downloadMbps;
        set { if (SetProperty(ref _downloadMbps, value)) OnPropertyChanged(nameof(SpeedText)); }
    }

    private double _uploadMbps;
    public double UploadMbps
    {
        get => _uploadMbps;
        set { if (SetProperty(ref _uploadMbps, value)) OnPropertyChanged(nameof(SpeedText)); }
    }

    private int _downloadLimitKbps;
    public int DownloadLimitKbps
    {
        get => _downloadLimitKbps;
        set { if (SetProperty(ref _downloadLimitKbps, value)) OnPropertyChanged(nameof(DownloadLimitText)); }
    }

    private int _uploadLimitKbps;
    public int UploadLimitKbps
    {
        get => _uploadLimitKbps;
        set { if (SetProperty(ref _uploadLimitKbps, value)) OnPropertyChanged(nameof(UploadLimitText)); }
    }

    private bool _isBlocked;
    public bool IsBlocked
    {
        get => _isBlocked;
        set => SetProperty(ref _isBlocked, value);
    }

    public string DownloadLimitText => DownloadLimitKbps <= 0 ? "∞" : $"{DownloadLimitKbps} kbps";
    public string UploadLimitText => UploadLimitKbps <= 0 ? "∞" : $"{UploadLimitKbps} kbps";
    public string SpeedText => $"{DownloadMbps:F2} / {UploadMbps:F2} Mbps";

    // --- Total usage (cumulative) — formatted for UI ---
    public string TotalDownloadText => FormatBytes(BytesReceived);
    public string TotalUploadText => FormatBytes(BytesSent);
    public string TotalUsageText => FormatBytes(BytesReceived + BytesSent);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}