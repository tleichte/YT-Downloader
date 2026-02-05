using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_DiscordBot.Pipes;

public class QueuePipeClient : PipeClient<QueueSchema.Entry, Result<AddToQueueFailReason>>
{
    protected override string PipeName => Config.Current.NamedPipeQueue;
    protected override PipeOptions PipeOptions => PipeOptions.None;
}