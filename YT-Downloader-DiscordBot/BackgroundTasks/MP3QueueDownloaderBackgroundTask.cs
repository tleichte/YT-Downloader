using System.Diagnostics;
using System.Text;
using ByteSizeLib;
using Microsoft.Extensions.Logging;
using YT_Downloader_DiscordBot.CF;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot.BackgroundTasks;

public class MP3QueueDownloaderBackgroundTask
{
    private VideoDownloadProgress m_progress;
    private VideoDownloadOutput m_output;
    private ILogger<BotWorker> m_logger;
    
    public void Start(ILogger<BotWorker> logger, CancellationToken stoppingToken)
    {
        m_logger = logger;
        Task.Run(async () =>
        {
            await Run(stoppingToken);
        }, stoppingToken);
    }
    
    
    private async Task Run(CancellationToken stoppingToken)
     {
         while (!stoppingToken.IsCancellationRequested)
         {
             try
             {
                 Database.Current.Mutex.WaitOne();
                 MP3QueueSchema.Entry? entry;
                 try
                 {
                     entry = await Database.Current.MP3QueueSchema.GetFirstEntry();
                 }
                 finally
                 {
                     Database.Current.Mutex.ReleaseMutex();
                 }
 
                 if (entry != null)
                 {
                     switch (entry.Value.Status)
                     {
                         case QueueStatus.Queued:
                         {
                             // Start video download
                             m_progress = new VideoDownloadProgress();
                             m_output = new VideoDownloadOutput($@"{Config.Current.DownloadOutputPath}\{entry.Value.UserId}-{entry.Value.MessageId}.txt");
                             await TryRunDownload(entry.Value, stoppingToken);
                             break;
                         }
                         case QueueStatus.Downloading:
                         {
                             if (m_progress != null)
                             {
                                 StringBuilder progressString = new();
                                 progressString.Append($"{(m_progress.Progress * 100):0.#}%");
                                 if (!string.IsNullOrEmpty(m_progress.ETA))
                                 {
                                     progressString.Append($" - ({m_progress.ETA})");
                                 }
 
                                 Database.Current.Mutex.WaitOne();
                                 try
                                 {
                                     await Database.Current.MP3QueueSchema.SetQueueProgress(entry.Value.UserId,
                                         entry.Value.Url,
                                         progressString.ToString());
                                 }
                                 finally
                                 {
                                     Database.Current.Mutex.ReleaseMutex();
                                 }
                             }
 
                             break;
                         }
                     }
                 }
             }
             catch (Exception ex)
             {
                 m_logger.LogError(ex, "Error while running Queue Background Task!");
             }
             
             await Task.Delay(1000, stoppingToken);
         }
     }
 
     private static async Task DoDownloadFail(MP3QueueSchema.Entry queueEntry, VideoDataResult? videoData, DownloadFailReason failReason)
     {
         Database.Current.Mutex.WaitOne();
         await Database.Current.MP3QueueSchema.RemoveFirstQueueEntry();
         Database.Current.Mutex.ReleaseMutex();
         
         AsyncQueues.MP3DownloadResults.Enqueue(MP3DownloadResult.CreateFail(queueEntry, videoData?.Data, failReason, []));
     }
 
     private static async Task DoDownloadSuccess(MP3QueueSchema.Entry queueEntry, VideoDataResult videoData)
     {
         Database.Current.Mutex.WaitOne();
         
         await Database.Current.MP3QueueSchema.RemoveFirstQueueEntry();
         await Database.Current.MP3LinksSchema.AddEntry(new MP3LinksSchema.Entry
         {
             UserId = queueEntry.UserId,
             VideoId = videoData.VideoId ?? string.Empty,
             Url = queueEntry.Url,
             MessageId = queueEntry.MessageId,
             DeleteAfter = DateTime.Now + TimeSpan.FromDays(Constants.MP3ExpirationDays)
         });
         
         Database.Current.Mutex.ReleaseMutex();
         
         AsyncQueues.MP3DownloadResults.Enqueue(MP3DownloadResult.CreateSuccess(queueEntry, videoData.Data));
     }
     
     private async Task TryRunDownload(MP3QueueSchema.Entry queueEntry, CancellationToken stoppingToken)
     {
         Database.Current.Mutex.WaitOne();
         await Database.Current.MP3QueueSchema.SetQueueStatus(QueueStatus.Fetching, queueEntry.UserId, queueEntry.Url);
         Database.Current.Mutex.ReleaseMutex();
         
         Directory.CreateDirectory(Helpers.GetMP3DiskFolderPathForUser(queueEntry.UserId));
         
         _ = Task.Run(async () =>
         {
             try
             {
                 VideoDataResult videoData = await Downloader.RunVideoDataFetchForURL(queueEntry.Url, m_output, stoppingToken);
 
                 if (!videoData.Success || videoData.VideoId == null || videoData.Data == null)
                 {
                     await DoDownloadFail(queueEntry, null, DownloadFailReason.VideoFetchError);
                     return;
                 }
 
                 if (videoData.Data.Duration >= Constants.MaxVideoDurationInSeconds)
                 {
                     await DoDownloadFail(queueEntry, videoData, DownloadFailReason.VideoTooLong);
                     return;
                 }
                 
                 string videoId = videoData.VideoId;
                 
                 Database.Current.Mutex.WaitOne();
                 MP3LinksSchema.Entry[] existingVideoIdEntries = await Database.Current.MP3LinksSchema.GetEntriesForVideoId(videoData.VideoId ?? string.Empty);
                 Database.Current.Mutex.ReleaseMutex();
 
                 if (existingVideoIdEntries.Length > 0)
                 {
                     if (existingVideoIdEntries.Any(e => e.UserId == queueEntry.UserId))
                     {
                         await DoDownloadFail(queueEntry, videoData, DownloadFailReason.VideoAlreadyDownloaded);
                         return;
                     }
                     
                     MP3LinksSchema.Entry existingEntry = existingVideoIdEntries[0];
                     string prevFilePath = Helpers.GetMP3DiskFilePath(existingEntry.UserId, videoId);
                     string newFilePath = Helpers.GetMP3DiskFilePath(queueEntry.UserId, videoId);
                     File.Copy(prevFilePath, newFilePath);
                     
                     await DoDownloadSuccess(queueEntry, videoData);
                     return;
                 }
                 
                 Database.Current.Mutex.WaitOne();
                 await Database.Current.MP3QueueSchema.SetQueueStatus(QueueStatus.Downloading, queueEntry.UserId, queueEntry.Url);
                 Database.Current.Mutex.ReleaseMutex();
                 
                 Downloader.VideoDownloadResult result = await Downloader.RunDownload(queueEntry.Url, true, m_progress, m_output, cancellationToken:stoppingToken);
                 if (!result.Result.Success)
                 {
                     await DoDownloadFail(queueEntry, videoData, result.Result.FailValue);
                     return;
                 }
 
                 Database.Current.Mutex.WaitOne();
                 await Database.Current.MP3QueueSchema.SetQueueStatus(QueueStatus.Processing, queueEntry.UserId, queueEntry.Url);
                 Database.Current.Mutex.ReleaseMutex();
 
                 string path = result.Path;
                 
                 string moveToPath = Helpers.GetMP3DiskFilePath(queueEntry.UserId, videoId);
                 File.Move(path, moveToPath);
                 
                 await DoDownloadSuccess(queueEntry, videoData);
             }
             catch (Exception e)
             {
                 m_logger.LogError(e, "Error while running video download!");
             }
         }, stoppingToken);
         
     }
 
     private enum ProcessingFailReason
     {
         None,
         NoProcess,
         ProcessFail,
     }
     
     private async ValueTask<Result<ProcessingFailReason>> RunProcessing(string path, string outputPath)
     {
         int errorLevel;
         ProcessStartInfo processInfo;
         Process? process;
 
         var arguments = new StringBuilder();
         arguments.Append("-ExecutionPolicy Bypass ");
         arguments.Append($"-File \"{Config.Current.ProcessingScriptPath}\" ");
         arguments.Append(string.Format(Config.Current.ProcessingScriptArguments, path, outputPath));
 
         processInfo = new ProcessStartInfo("powershell.exe", arguments.ToString())
         {
             CreateNoWindow = true,
             UseShellExecute = false,
             RedirectStandardOutput = true,
         };
         
         process = Process.Start(processInfo);
         if (process == null)
         {
             return Result.MakeFail(ProcessingFailReason.NoProcess);
         }
 
         await Task.Run(() =>
         {
             process.WaitForExit();
         });
 
         errorLevel = process.ExitCode;
         process.Close();
 
         if (errorLevel != 0)
         {
             return Result.MakeFail(ProcessingFailReason.ProcessFail);
         }
         
         return Result.MakeSuccess<ProcessingFailReason>();
     }
}