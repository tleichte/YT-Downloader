using System.IO.Pipes;
using System.Text;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace YT_Downloader_DiscordBot;

public static class BotHelpers
{

    public static string GetQueueMessage(QueueSchema.Entry entry)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## {entry.Name}");

        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{entry.Url}`");
        
        string statusString;
        switch (entry.Status)
        {
            case QueueStatus.Queued:
                statusString = "QUEUED";
                break;
            case QueueStatus.Downloading:
            case QueueStatus.Copying:
                statusString = $"IN PROGRESS - {entry.Progress}";
                break;
            case QueueStatus.Throttled:
                statusString = "THROTTLED - DELAYING...";
                break;
            default:
                statusString = "UNKNOWN";
                break;
        }
        
        builder.AppendLine($"### Status: _{statusString}_");
        
        return builder.ToString();
    }

    public static string GetLinkMessageString(LinksSchema.Entry entry, LinkMetadataSchema.Entry metadata)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"**{entry.Name}**");

        builder.AppendLine($"**Original:**");

        List<string> titleStrings = [];
        if (!string.IsNullOrEmpty(metadata.VideoAuthor))
        {
            titleStrings.Add(metadata.VideoAuthor);
        }
        if (!string.IsNullOrEmpty(metadata.VideoTitle))
        {
            titleStrings.Add(metadata.VideoTitle);
        }
        if (titleStrings.Count > 0)
        {
            builder.AppendLine($"_{string.Join(" - ", titleStrings)}_");
        }

        builder.AppendLine($"`{metadata.OriginalUrl}`");

        if (entry.DeleteAfter != DateTime.MaxValue)
        {
            builder.AppendLine($"Expires: <t:{(int)(entry.DeleteAfter - DateTime.UnixEpoch).TotalSeconds}:f>");
        }

        builder.AppendLine($"```{Helpers.GetServerUrl(entry.UserId, entry.VideoId)}```");
        
        return builder.ToString();
    }
    
    
    public static async Task UpdateQueueMessage(DiscordClient client, ulong userId, string name)
    {
        QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetEntryForName(userId, name);
        if (entry == null)
        {
            return;
        }
        
        DiscordUser user = await client.GetUserAsync(userId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);
        
        var builder = new StringBuilder();

        
        await message.ModifyAsync(builder.ToString());
    }
    
    
    public static void CreateMessageUpdatePipeServer()
    {
        Task.Run(async () =>
        {
            var server = new NamedPipeServerStream(Config.Current.NamedPipeQueueName, PipeDirection.In);
            while (true)
            {
                try
                {
                    await server.WaitForConnectionAsync();
                    var reader = new StreamReader(server);
                    var writer = new StreamWriter(server);
                    
                    string? line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        continue;
                    }
                    
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                
            }
        });
    }

    public static async ValueTask<Result<AddToQueueFailReason>> SendQueueEntry(QueueSchema.Entry entry)
    {
        var client =
            new NamedPipeClientStream(".", Config.Current.NamedPipeQueueName, PipeDirection.InOut, PipeOptions.None);
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