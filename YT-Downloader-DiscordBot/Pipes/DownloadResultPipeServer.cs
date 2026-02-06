using System.Collections.Concurrent;
using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_DiscordBot.Pipes;

public class DownloadResultPipeServer : PipeServer<DownloadResult, bool>
{

    public static readonly ConcurrentQueue<DownloadResult> Queue = new();
    
    protected override string PipeName => Config.Current.NamedPipeDownloadResult;
    protected override ValueTask<bool> Handle(DownloadResult output)
    {
        Queue.Enqueue(output);
        return ValueTask.FromResult(true);
    }
}