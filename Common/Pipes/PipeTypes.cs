namespace Common;


public struct DownloadResult
{
    public ulong UserId;
    public string Name;
    public string OriginalUrl;
    public ulong MessageId;

    public Result<DownloadFailReason> Result;
}