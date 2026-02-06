using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;

namespace YT_Downloader_Service.Pipes;

public class QueuePipeServer : PipeServer<QueueSchema.Entry, Result<AddToQueueFailReason>>
{
    protected override string PipeName => Config.Current.NamedPipeQueue;
    protected override async ValueTask<Result<AddToQueueFailReason>> Handle(QueueSchema.Entry output)
    {
        Database.Instance.Mutex.WaitOne();
        try
        {
            await Database.Instance.QueueSchema.AddEntry(output);
            return Result.MakeSuccess<AddToQueueFailReason>();
        }
        catch (Exception e)
        {
            return Result.MakeFail(AddToQueueFailReason.FailedDb);
        }
        finally
        {
            Database.Instance.Mutex.ReleaseMutex();
        }
    }
}