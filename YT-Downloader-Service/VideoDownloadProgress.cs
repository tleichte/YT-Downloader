using YoutubeDLSharp;

namespace YT_Downloader_Service;

public class VideoDownloadProgress : IProgress<DownloadProgress>
{
    
    public float Progress { get; private set; }
    public string ETA { get; private set; }
    
    public void Report(DownloadProgress value)
    {
        Progress = value.Progress;
        ETA = value.ETA;
    }
}