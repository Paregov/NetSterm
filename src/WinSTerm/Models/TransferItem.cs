namespace WinSTerm.Models;

public enum TransferDirection { Upload, Download }
public enum TransferStatus { Queued, InProgress, Completed, Failed, Cancelled }

public class TransferItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string FileName { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string RemotePath { get; set; } = "";
    public TransferDirection Direction { get; set; }
    public long TotalBytes { get; set; }

    private long _transferredBytes;
    public long TransferredBytes
    {
        get => _transferredBytes;
        set { SetProperty(ref _transferredBytes, value); OnPropertyChanged(nameof(ProgressPercent)); }
    }

    private TransferStatus _status = TransferStatus.Queued;
    public TransferStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double ProgressPercent => TotalBytes > 0 ? (double)TransferredBytes / TotalBytes * 100 : 0;

    public bool IsUpload => Direction == TransferDirection.Upload;
    public bool IsDownload => Direction == TransferDirection.Download;
}
