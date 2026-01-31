using System.IO.Pipes;
using Common;
using Common.DatabaseSchemas;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace YT_Downloader_DiscordBot;

public static class BotHelpers
{


    public static async ValueTask<Result<AddToQueueFailReason>> SendQueueEntry(QueueSchema.Entry entry)
    {
        var client =
            new NamedPipeClientStream(".", Config.Current.NamedPipeName, PipeDirection.InOut, PipeOptions.None);
        if (!client.IsConnected)
        {
            await client.ConnectAsync();
        }

        var reader = new StreamReader(client);
        var writer = new StreamWriter(client);

        try
        {
            await writer.WriteLineAsync(JsonConvert.SerializeObject(entry));
            await writer.FlushAsync();
            
            // Only runnable on windows, disabling warning
#pragma warning disable CA1416
            client.WaitForPipeDrain();
#pragma warning restore CA1416

            string? line = await reader.ReadLineAsync();
            if (line != null)
            {
                return JsonConvert.DeserializeObject<Result<AddToQueueFailReason>>(line);
            }
        }
        catch (Exception e)
        {
            // passthrough
        }

        return Result.MakeFail(AddToQueueFailReason.PipeFail);
    }
}