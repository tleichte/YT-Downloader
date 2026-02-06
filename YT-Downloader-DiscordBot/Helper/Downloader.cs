using System.Collections;
using Common;
using Common.DatabaseSchemas;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace YT_Downloader_Service;

public class Downloader
{
    private const string FormatString = "best[height<=1080]";

    private string GetVideoId(VideoData videoData)
    {
        return $"{videoData.Extractor}-{videoData.ID}";
    }

    public struct InnerVideoDownloadResult
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
    
    public async ValueTask<VideoDownloadResult> RunDownload(string url, VideoDownloadProgress progress, CancellationToken cancellationToken)
    {

        var ytDl = new YoutubeDL
        {
            OutputFolder = Config.Current.TempDownloadsPath
        };

        var dataResult = await ytDl.RunVideoDataFetch(url, ct:cancellationToken);
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
        
        var options = new OptionSet()
        {
            NoContinue = true,
            RestrictFilenames = true,
            RecodeVideo = VideoRecodeFormat.Mp4,
            Format = FormatString,
        };
        
        InnerVideoDownloadResult result = await RunDownloadInner(ytDl, url, options, progress, cancellationToken);
        if (!result.Result.Success)
        {
            if (result.Result.FailValue != DownloadFailReason.VideoBotError)
            {
                return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);

            }
            
            // Retry with cookies if download fail reason is because of a bot detect

            options.Cookies = Config.Current.CookiesPath;
            result = await RunDownloadInner(ytDl, url, options, progress, cancellationToken);
            if (!result.Result.Success)
            {
                return VideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
            }
        }
        
        return VideoDownloadResult.MakeSuccess(result.Path, metadata);
    }


    private async ValueTask<InnerVideoDownloadResult> RunDownloadInner(YoutubeDL ytDl, string url, OptionSet optionSet, VideoDownloadProgress progress, CancellationToken cancellationToken)
    {
        RunResult<string>? res = await ytDl.RunVideoDownload(url, overrideOptions: optionSet, progress:progress, ct: cancellationToken);
        if (!res.Success)
        {
            string[] errorOutput = res.ErrorOutput;
            if (errorOutput.Length == 1 && errorOutput[0].Contains("Sign in"))
            {
                return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoBotError);
            }

            return InnerVideoDownloadResult.MakeFail(DownloadFailReason.VideoDownloadError);
        }
        return InnerVideoDownloadResult.MakeSuccess(res.Data);
    }
    
}