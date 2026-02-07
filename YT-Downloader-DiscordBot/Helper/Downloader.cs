using Common;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_Service;

namespace YT_Downloader_DiscordBot.Helper;

public struct DownloadResult
{
    public ulong UserId;
    public string Name;
    public string OriginalUrl;
    public ulong MessageId;
    public string? VideoId;

    public Result<DownloadFailReason> Result;

    public static DownloadResult CreateSuccess(QueueSchema.Entry entry, VideoData? videoData) => new()
    {
        UserId = entry.UserId,
        Name = entry.Name,
        OriginalUrl = entry.Url,
        MessageId = entry.MessageId,
        VideoId = videoData != null ? Downloader.GetVideoId(videoData) : null,
        Result = Common.Result.MakeSuccess<DownloadFailReason>()
    };
    
    
    public static DownloadResult CreateFail(QueueSchema.Entry entry, VideoData? videoData, DownloadFailReason failReason) => new()
    {
        UserId = entry.UserId,
        Name = entry.Name,
        OriginalUrl = entry.Url,
        MessageId = entry.MessageId,
        VideoId = videoData != null ? Downloader.GetVideoId(videoData) : null,
        Result = Common.Result.MakeFail(failReason)
    };
}

public class VideoDataResult
{
    public bool Success { get; set; }
    public string? VideoId { get; set; }
    public VideoData? Data { get; set; }

    public static VideoDataResult MakeSuccess(VideoData data) => new()
    {
        Success = true,
        VideoId = Downloader.GetVideoId(data),
        Data = data,
    };

    public static VideoDataResult MakeFail() => new()
    {
        Success = false,
        VideoId = null,
        Data = null,
    };

}

public class Downloader
{
    private const string FormatString = "best[height<=1080]";

    public static async ValueTask<VideoDataResult> RunVideoDataFetchForURL(string url, CancellationToken cancellationToken)
    {
        var ytDl = new YoutubeDL
        {
            OutputFolder = Config.Current.TempDownloadsPath
        };

        var dataResult = await ytDl.RunVideoDataFetch(url, ct:cancellationToken);
        if (!dataResult.Success)
        {
            return VideoDataResult.MakeFail(); 
        }
        return VideoDataResult.MakeSuccess(dataResult.Data);
    }
    
    public static string GetVideoId(VideoData videoData)
    {
        return $"{videoData.Extractor}-{videoData.ID}";
    }

    private struct InnerVideoDownloadResult
    {
        public Result<DownloadFailReason> Result { get; private set; }
        public string Path { get; private set; }
        
        public static InnerVideoDownloadResult MakeSuccess(string path)
        {
            return new InnerVideoDownloadResult
            {
                Result = Common.Result.MakeSuccess<DownloadFailReason>(),
                Path = path,
            };
        }
        
        public static InnerVideoDownloadResult MakeFail(DownloadFailReason reason)
        {
            return new InnerVideoDownloadResult
            {
                Result = Common.Result.MakeFail(reason)
            };
        }
    }
    
    public struct VideoDownloadResult
    {
        public Result<DownloadFailReason> Result { get; private set; }
        public string Path { get; private set; }

        public static VideoDownloadResult MakeFail(DownloadFailReason reason)
        {
            return new VideoDownloadResult
            {
                Result = Common.Result.MakeFail(reason),
                Path = "",
            };
        }

        public static VideoDownloadResult MakeSuccess(string path)
        {
            return new VideoDownloadResult
            {
                Result = Common.Result.MakeSuccess<DownloadFailReason>(),
                Path = path,
            };
        }
    }
    
    public static async ValueTask<VideoDownloadResult> RunDownload(string url, VideoDownloadProgress progress, CancellationToken cancellationToken)
    {

        var ytDl = new YoutubeDL
        {
            OutputFolder = Config.Current.TempDownloadsPath
        };
        
        var options = new OptionSet()
        {
            NoContinue = true,
            RestrictFilenames = true,
            RecodeVideo = VideoRecodeFormat.Mp4,
            Format = FormatString,
        };
        
        InnerVideoDownloadResult result = await RunDownloadInner(ytDl, url, options, progress, cancellationToken);
        if (result.Result.Success)
        {
            return VideoDownloadResult.MakeSuccess(result.Path);
        }
        
        
        if (result.Result.FailValue != DownloadFailReason.VideoBotError)
        {
            return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
        }
            
        // Retry with cookies if download fail reason is because of a bot detect
        
        options.Cookies = Config.Current.CookiesPath;
        result = await RunDownloadInner(ytDl, url, options, progress, cancellationToken);
        if (result.Result.Success)
        {
            return VideoDownloadResult.MakeSuccess(result.Path);
        }

        return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
    }


    private static async ValueTask<InnerVideoDownloadResult> RunDownloadInner(YoutubeDL ytDl, string url, OptionSet optionSet, VideoDownloadProgress progress, CancellationToken cancellationToken)
    {
        RunResult<string>? res = await ytDl.RunVideoDownload(url, overrideOptions: optionSet, progress:progress, ct: cancellationToken);
        if (res.Success)
        {
            return InnerVideoDownloadResult.MakeSuccess(res.Data);
        }
        
        string[] errorOutput = res.ErrorOutput;
        if (errorOutput.Length == 1 && errorOutput[0].Contains("Sign in"))
        {
            return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoBotError);
        }

        return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
    }
    
}