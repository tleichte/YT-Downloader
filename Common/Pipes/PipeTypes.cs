namespace Common;


public struct DownloadResultPipeMessage
{
    public ulong UserId;
    public string Name;

    public Result<DownloadFailReason> Result;
}