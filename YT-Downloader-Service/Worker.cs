using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;
using Newtonsoft.Json;

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
            m_databaseMutex.WaitOne();
            
            await ServiceHelpers.DeleteExpiredLinks();

            QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetFirstEntry();
            
            m_databaseMutex.ReleaseMutex();
            
            if (entry != null)
            {
                TryRunDownload(entry.Value, stoppingToken);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }

    private void TryRunDownload(QueueSchema.Entry entry, CancellationToken stoppingToken)
    {
        if (entry.Status == QueueStatus.Queued 
            || (entry.Status == QueueStatus.Throttled && IsPastLastThrottleTime()))
        {
            Task _ = Task.Run(async () =>
            {
                var downloader = new Downloader();
                Downloader.VideoDownloadResult result = await downloader.RunDownload(entry.Url, cancellationToken:stoppingToken);
                if (result.Result.Success)
                {
                    string moveToPath = Helpers.GetVideoDiskPath(entry.UserId, result.Metadata.VideoId);
                    File.Move(result.Path, moveToPath);
                            
                    m_databaseMutex.WaitOne();

                    await Database.Instance.QueueSchema.RemoveFirstQueueEntry();
                            
                    m_databaseMutex.ReleaseMutex();
                }
                else
                {
                    if (result.Result.FailValue == Downloader.DownloadFailReason.VideoDownloadError)
                    {
                        m_lastThrottleTime = DateTime.Now;
                        await Database.Instance.QueueSchema.SetQueueStatus(QueueStatus.Throttled, entry.UserId, entry.Name);
                    }
                }
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
        // Bot sends queue entry -> service adds to db queue -> service sends same queue entry back 
        Task.Run(async () =>
        {
            var server = new NamedPipeServerStream(Config.Current.NamedPipeName, PipeDirection.InOut);
            StreamWriter writer = new(server);
            StreamReader reader = new(server);
            
            while (true)
            {
                try
                {
                    await server.WaitForConnectionAsync();
                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        continue;
                    }
                    QueueSchema.Entry? entry = JsonConvert.DeserializeObject<QueueSchema.Entry>(line);
                    if (entry == null)
                    {
                        continue;
                    }
                    
                    m_databaseMutex.WaitOne();

                    Result<AddToQueueFailReason> result;
                    
                    try
                    {
                        await Database.Instance.QueueSchema.ExecuteInsert(entry.Value);
                        result = Result.MakeSuccess<AddToQueueFailReason>();
                    }
                    catch (Exception)
                    {
                        result = Result.MakeFail(AddToQueueFailReason.FailedDb);
                    }

                    m_databaseMutex.ReleaseMutex();
                        
                    await writer.WriteLineAsync(JsonConvert.SerializeObject(result));
                    await writer.FlushAsync();
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    // This will only ever run on windows, disable warning
#pragma warning disable CA1416 
                    server.WaitForPipeDrain();
#pragma warning restore CA1416
                    if (server.IsConnected)
                    {
                        server.Disconnect();
                    }
                }
            }
        });
    }
}