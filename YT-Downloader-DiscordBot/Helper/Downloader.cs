using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB.Schemas;

namespace YT_Downloader_DiscordBot.Helper;

public struct DownloadResult
{
    public ulong UserId;
    public string Name;
    public string OriginalUrl;
    public ulong MessageId;
    public string? VideoId;

    public Result<DownloadFailReason> Result;
    public string[] ErrorMessages;

    public static DownloadResult CreateSuccess(QueueSchema.Entry entry, VideoData? videoData) => new()
    {
        UserId = entry.UserId,
        Name = entry.Name,
        OriginalUrl = entry.Url,
        MessageId = entry.MessageId,
        VideoId = videoData != null ? Downloader.GetVideoId(videoData) : null,
        Result = Helper.Result.MakeSuccess<DownloadFailReason>(),
        ErrorMessages = []
    };
    
    
    public static DownloadResult CreateFail(QueueSchema.Entry entry, VideoData? videoData, DownloadFailReason failReason, string[] errorMessages) => new()
    {
        UserId = entry.UserId,
        Name = entry.Name,
        OriginalUrl = entry.Url,
        MessageId = entry.MessageId,
        VideoId = videoData != null ? Downloader.GetVideoId(videoData) : null,
        Result = Helper.Result.MakeFail(failReason),
        ErrorMessages = errorMessages
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

    public static async ValueTask<VideoDataResult> RunVideoDataFetchForURL(string url, VideoDownloadOutput output, CancellationToken cancellationToken)
    {
        var ytDl = new YoutubeDL
        {
            OutputFolder = Config.Current.TempDownloadsPath
        };

        const int MAX_TRIES = 2;
        var runCount = 0;
        while (++runCount <= MAX_TRIES)
        {
            var usedCookies = false;
            OptionSet options = new();
            if (Config.Current.ShouldAlwaysUseCookies || runCount > 1)
            {
                options.Cookies = Config.Current.CookiesPath;
                usedCookies = true;
            }
            
            RunResult<VideoData>? dataResult = await ytDl.RunVideoDataFetch(url, overrideOptions:options, ct:cancellationToken);
            if (dataResult.Success)
            {
                return VideoDataResult.MakeSuccess(dataResult.Data);
            }
            
            foreach (string error in dataResult.ErrorOutput)
            {
                output.Report(error);
            }
            
            if (usedCookies || !dataResult.ErrorOutput.Any(s => s.Contains("Sign in")))
            {
                return VideoDataResult.MakeFail();
            }
        }
        
        return VideoDataResult.MakeFail();
    }
    
    public static string GetVideoId(VideoData videoData)
    {
        return $"{videoData.Extractor}-{videoData.ID}";
    }

    private struct InnerVideoDownloadResult
    {
        public Result<DownloadFailReason> Result { get; private set; }
        public string Path { get; private set; }
        public string[] ErrorMessages { get; private set; }
        
        public static InnerVideoDownloadResult MakeSuccess(string path)
        {
            return new InnerVideoDownloadResult
            {
                Result = Helper.Result.MakeSuccess<DownloadFailReason>(),
                Path = path,
                ErrorMessages = []
            };
        }
        
        public static InnerVideoDownloadResult MakeFail(DownloadFailReason reason, string[] errorMessages)
        {
            return new InnerVideoDownloadResult
            {
                Result = Helper.Result.MakeFail(reason),
                ErrorMessages = errorMessages
            };
        }
    }
    
    public struct VideoDownloadResult
    {
        public Result<DownloadFailReason> Result { get; private set; }
        public string Path { get; private set; }
        public string[] ErrorMessages { get; private set; }

        public static VideoDownloadResult MakeFail(DownloadFailReason reason, string[] errorMessages)
        {
            return new VideoDownloadResult
            {
                Result = Helper.Result.MakeFail(reason),
                Path = "",
                ErrorMessages = errorMessages,
            };
        }

        public static VideoDownloadResult MakeSuccess(string path)
        {
            return new VideoDownloadResult
            {
                Result = Helper.Result.MakeSuccess<DownloadFailReason>(),
                Path = path,
                ErrorMessages = []
            };
        }
    }
    
    public static async ValueTask<VideoDownloadResult> RunDownload(string url, VideoDownloadProgress progress, VideoDownloadOutput output, CancellationToken cancellationToken)
    {
        var ytDl = new YoutubeDL
        {
            OutputFolder = Config.Current.TempDownloadsPath
        };
        
        const int MAX_TRIES = 2;
        int runCount = 0;
        while (++runCount <= MAX_TRIES)
        {
            var options = new OptionSet()
            {
                NoContinue = true,
                RestrictFilenames = true,
                RecodeVideo = VideoRecodeFormat.Mp4,
                Format = FormatString,
            };

            var usedCookies = false;
            if (Config.Current.ShouldAlwaysUseCookies || runCount > 1)
            {
                options.Cookies = Config.Current.CookiesPath;
                usedCookies = true;
            }
            
            InnerVideoDownloadResult result = await RunDownloadInner(ytDl, url, options, progress, output, cancellationToken);
            
            if (result.Result.Success)
            {
                output.RemoveOutput();
                return VideoDownloadResult.MakeSuccess(result.Path);
            }
            
            // Failed after using cookies or not from bot detection, return out of here 
            if (usedCookies || result.Result.FailValue != DownloadFailReason.VideoBotError)
            {
                return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError, result.ErrorMessages);
            }
        }
        return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError, []);
    }


    private static async ValueTask<InnerVideoDownloadResult> RunDownloadInner(YoutubeDL ytDl, string url, OptionSet optionSet, VideoDownloadProgress progress, VideoDownloadOutput output, CancellationToken cancellationToken)
    {
        RunResult<string>? res = await ytDl.RunVideoDownload(url,overrideOptions:optionSet, progress:progress, output:output, ct: cancellationToken);
        if (res.Success)
        {
            return InnerVideoDownloadResult.MakeSuccess(res.Data);
        }
        
        string[] errorOutput = res.ErrorOutput;

        // Special case - sometimes data blocks aren't "found" and video is still fine. Return true in this case
        if (errorOutput.All(s => string.IsNullOrEmpty(s) || s.Contains("ERROR: Did not get any data blocks")))
        {
            return InnerVideoDownloadResult.MakeSuccess(res.Data);
        }
        
        if (File.Exists(res.Data))
        {
            File.Delete(res.Data);
        }
        
        if (errorOutput.Any(s => s.Contains("Sign in")))
        {
            return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoBotError, errorOutput);
        }

        return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError, errorOutput);
    }
    
}