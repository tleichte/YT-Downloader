using System.Collections;
using Common;
using Common.DatabaseSchemas;
using FFMpegCore;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace YT_Downloader_Service;

public class Downloader
{
    private string m_outputFolderPath;
    private string m_cookiesPath;

    private const string FormatString = "best[height<=1080]";

    private string GetVideoId(VideoData videoData)
    {
        return $"{videoData.Extractor}-{videoData.ID}";
    }

    public enum DownloadFailReason
    {
        None,
        VideoFetchError,
        VideoDownloadError,
        ResolutionMismatch
    }

    public struct VideoDownloadResult
    {
        public Result<DownloadFailReason> Result { get; private set; }
        public string Path { get; private set; }
        public LinkMetadataSchema.Entry Metadata { get; private set; }

        public static VideoDownloadResult MakeFail(DownloadFailReason reason)
        {
            return new VideoDownloadResult
            {
                Result = Common.Result.MakeFail(reason),
                Path = "",
                Metadata = default
            };
        }

        public static VideoDownloadResult MakeSuccess(string path, LinkMetadataSchema.Entry metadata)
        {
            return new VideoDownloadResult
            {
                Result = Common.Result.MakeSuccess<DownloadFailReason>(),
                Path = path,
                Metadata = metadata,
            };
        }
    }
    
    public async ValueTask<VideoDownloadResult> RunDownload(string url, CancellationToken cancellationToken)
    {

        var ytDL = new YoutubeDL();
        ytDL.OutputFolder = m_outputFolderPath;
        
        var dataResult = await ytDL.RunVideoDataFetch(url, ct:cancellationToken);
        if (!dataResult.Success)
        {
            return VideoDownloadResult.MakeFail(DownloadFailReason.VideoFetchError);
        }

        
        string videoId = GetVideoId(dataResult.Data);
        var metadata = new LinkMetadataSchema.Entry
        {
            VideoId = videoId,
            VideoTitle = dataResult.Data.Title,
            VideoAuthor = dataResult.Data.Channel,
            OriginalUrl = url,
            Duration = dataResult.Data.Duration.GetValueOrDefault(0)
        };
        
        
        int maxHeight = int.Min(dataResult.Data.Formats.Max(f => f.Height) ?? 0, 1080);

        var options = new OptionSet()
        {
            NoContinue = true,
            RestrictFilenames = true,
            RecodeVideo = VideoRecodeFormat.Mp4,
            Format = FormatString,
            Cookies = m_cookiesPath,
            Paths = Config.Current.QueueTempPath
        };
        
        RunResult<string>? res = await ytDL.RunVideoDownload(url, overrideOptions: options, ct: cancellationToken);
        if (!res.Success)
        {
            string[] errorOutput = res.ErrorOutput;
            return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
        }
        
        string path = res.Data;
        IMediaAnalysis mediaInfo = await FFProbe.AnalyseAsync(path, cancellationToken: cancellationToken);
        int height = mediaInfo.PrimaryVideoStream?.Height ?? 0;
        if (height != maxHeight)
        {
            File.Delete(path);
            return VideoDownloadResult.MakeFail(DownloadFailReason.ResolutionMismatch);
        }
        
        return VideoDownloadResult.MakeSuccess(path, metadata);
    }
    
    
}