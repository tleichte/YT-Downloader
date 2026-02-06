using System.IO.Pipes;
using Common;

namespace YT_Downloader_Service.Pipes;

public class DownloadResultPipeClient : PipeClient<DownloadResult, bool>
{
    protected override string PipeName => Config.Current.NamedPipeDownloadResult;
    protected override PipeOptions PipeOptions => PipeOptions.None;
}