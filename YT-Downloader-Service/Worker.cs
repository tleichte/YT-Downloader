using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;
using Newtonsoft.Json;
using YT_Downloader_Service.Pipes;

namespace YT_Downloader_Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> m_logger;

    private readonly Mutex m_databaseMutex = new();
    
    private readonly Mutex m_throttleTimeMutex = new();
    private DateTime m_lastThrottleTime = DateTime.MinValue;
    
    public Worker(ILogger<Worker> logger)
    {
        m_logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Config.Create();
        
        Subscribe();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            Database.Instance.Mutex.WaitOne();
            
            await ServiceHelpers.DeleteExpiredLinks();

            QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetFirstEntry();
            
            Database.Instance.Mutex.ReleaseMutex();
            
            if (entry != null)
            {
                TryRunDownload(entry.Value, stoppingToken);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private void TryRunDownload(QueueSchema.Entry queueEntry, CancellationToken stoppingToken)
    {
        if (queueEntry.Status == QueueStatus.Queued 
            || (queueEntry.Status == QueueStatus.Throttled && IsPastLastThrottleTime()))
        {
            Task _ = Task.Run(async () =>
            {
                var downloader = new Downloader();
                Downloader.VideoDownloadResult result = await downloader.RunDownload(queueEntry.Url, cancellationToken:stoppingToken);
                if (result.Result.Success)
                {
                    string moveToPath = Helpers.GetVideoDiskPath(queueEntry.UserId, result.Metadata.VideoId);
                    File.Move(result.Path, moveToPath);

                    Database.Instance.Mutex.WaitOne();

                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                    await Database.Instance.LinksSchema.AddEntry(new LinksSchema.Entry
                    {
                        UserId = queueEntry.UserId,
                        Name = queueEntry.Name,
                        VideoId = result.Metadata.VideoId,
                        MessageId = queueEntry.MessageId,
                        DeleteAfter = queueEntry.Persistent ? DateTime.MaxValue : DateTime.Now + TimeSpan.FromDays(Config.Current.ExpirationDays)
                    });
                    await Database.Instance.LinkMetadataSchema.AddEntry(result.Metadata);
                    
                    Database.Instance.Mutex.ReleaseMutex();
                }
                else
                {
                    if (result.Result.FailValue == DownloadFailReason.VideoDownloadError)
                    {
                        m_lastThrottleTime = DateTime.Now;
                        await Database.Instance.QueueSchema.SetQueueStatus(QueueStatus.Throttled, queueEntry.UserId, queueEntry.Name);
                    }
                }
                
                await new DownloadResultPipeClient().Send(new DownloadResultPipeMessage
                {
                    UserId = queueEntry.UserId,
                    Name = queueEntry.Name,
                    Result = result.Result,
                });
            }, stoppingToken);
        }
    }
    
    private bool IsPastLastThrottleTime()
    {
        m_throttleTimeMutex.WaitOne();
        
        TimeSpan ts = DateTime.Now - m_lastThrottleTime;

        m_throttleTimeMutex.ReleaseMutex();

        return ts.TotalMinutes >= Config.Current.ThrottleRetryTime;
    }
    
    private void Subscribe()
    {
        QueuePipeServer server = new();
        server.Start();
        
        // Bot sends queue entry -> service adds to db queue -> service sends same queue entry back 
//         Task.Run(async () =>
//         {
//             var server = new NamedPipeServerStream(Config.Current.NamedPipeQueue, PipeDirection.InOut);
//             
//             while (true)
//             {
//                 try
//                 {
//                     await server.WaitForConnectionAsync();
//                     
//                     StreamWriter writer = new(server);
//                     StreamReader reader = new(server);
//                     string? line = await reader.ReadLineAsync();
//                     if (line == null)
//                     {
//                         continue;
//                     }
//                     QueueSchema.Entry? entry = JsonConvert.DeserializeObject<QueueSchema.Entry>(line);
//                     if (entry == null)
//                     {
//                         continue;
//                     }
//                     
//                     m_databaseMutex.WaitOne();
//
//                     Result<AddToQueueFailReason> result;
//                     
//                     try
//                     {
//                         await Database.Instance.QueueSchema.AddEntry(entry.Value);
//                         result = Result.MakeSuccess<AddToQueueFailReason>();
//                     }
//                     catch (Exception)
//                     {
//                         result = Result.MakeFail(AddToQueueFailReason.FailedDb);
//                     }
//
//                     m_databaseMutex.ReleaseMutex();
//                         
//                     await writer.WriteLineAsync(JsonConvert.SerializeObject(result));
//                     await writer.FlushAsync();
//                 }
//                 catch (Exception e)
//                 {
//                     throw e;
//                 }
//                 finally
//                 {
//                     // This will only ever run on windows, disable warning
// #pragma warning disable CA1416 
//                     server.WaitForPipeDrain();
// #pragma warning restore CA1416
//                     if (server.IsConnected)
//                     {
//                         server.Disconnect();
//                     }
//                 }
//             }
//         });
    }
    
    
    
}