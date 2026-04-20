using DSharpPlus.Entities;

namespace YT_Downloader_DiscordBot.Helper;

public enum QueueStatus
{
    Queued,
    Fetching,
    Downloading,
    Processing
}

public enum DownloadFailReason
{
    None,
    VideoFetchError,
    VideoTooLong,
    VideoBotError,
    VideoDownloadError,
    VideoAlreadyDownloaded,
}

public static class Result
{
    public static Result<TFail> MakeSuccess<TFail>() where TFail : struct
    {
        return new Result<TFail>
        {
            Success = true,
            FailValue = default
        };
    }
    
    public static Result<TFail> MakeFail<TFail>(TFail failValue) where TFail : struct
    {
        return new Result<TFail>
        {
            Success = false,
            FailValue = failValue
        };
    }
}

public struct Result<TFail> where TFail : struct
{
    public bool Success;
    public TFail FailValue;
    
    public static implicit operator bool(Result<TFail> result)
    {
        return result.Success;
    }
}

public enum ExpirationPolicy
{
    Standard,
    Never
}

public struct AddToQueueParameters
{
    public string Name;
    public string URL;
    public bool Persistent;
    public DiscordUser User;
    
}