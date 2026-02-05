using System.IO.Pipes;
using System.Text;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using YT_Downloader_DiscordBot.Pipes;

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
    
    
    public static async Task TryUpdateQueueMessage(DiscordClient client)
    {
        QueueSchema.Entry? entry = await Database.Instance.QueueSchema.GetFirstEntry();
        if (entry == null || entry.Value.Status == QueueStatus.Queued)
        {
            return;
        }
        
        DiscordUser user = await client.GetUserAsync(entry.Value.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);
        
        string messageString = GetQueueMessage(entry.Value);
        
        await message.ModifyAsync(messageString);
    }

    public static async Task CheckForDownloadResults(DiscordClient client)
    {
        while (!DownloadResultPipeServer.Queue.IsEmpty)
        {
            DownloadResultPipeServer.Queue.TryDequeue(out DownloadResultPipeMessage downloadResult);
            await UpdateResultMessage(client, downloadResult);
        }
    }

    public static async Task UpdateResultMessage(DiscordClient client, DownloadResultPipeMessage downloadResult)
    {
        LinksSchema.Entry? entry = await Database.Instance.LinksSchema.GetEntryForName(downloadResult.UserId, downloadResult.Name);
        if (entry == null)
        {
            return;
        }

        LinkMetadataSchema.Entry? metadata =
            await Database.Instance.LinkMetadataSchema.GetMetadataForVideo(entry.Value.VideoId);
        if (metadata == null)
        {
            return;
        }
        
        DiscordUser user = await client.GetUserAsync(entry.Value.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(entry.Value.MessageId);
        
        string messageString = GetLinkMessageString(entry.Value, metadata.Value);
        
        await message.ModifyAsync(messageString);
    }
}