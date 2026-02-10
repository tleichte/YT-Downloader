using YoutubeDLSharp;

namespace YT_Downloader_DiscordBot;

public class VideoDownloadProgress : IProgress<DownloadProgress>
{
    
    public float Progress { get; private set; }
    public string ETA { get; private set; }
    
    public void Report(DownloadProgress value)
    {
        Progress = GetProgressValue(value);
        ETA = value.ETA;
    }

    private float GetProgressValue(DownloadProgress progress)
    {
        switch (progress.State)
        {
            case DownloadState.None:
            case DownloadState.PreProcessing:
            case DownloadState.Error:
            default:
                return 0;
            case DownloadState.Downloading:
                return progress.Progress;
            case DownloadState.PostProcessing:
            case DownloadState.Success:
                return 1;
        }
    }
}