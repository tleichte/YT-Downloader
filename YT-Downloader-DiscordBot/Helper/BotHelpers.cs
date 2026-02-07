using System.Text;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus;
using DSharpPlus.Entities;

namespace YT_Downloader_DiscordBot.Helper;

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
            default:
                statusString = "UNKNOWN";
                break;
        }
        
        builder.AppendLine($"### Status: _{statusString}_");
        
        return builder.ToString();
    }

    public static string GetLinkFailedMessage(DownloadResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"## {result.Name}");
        builder.AppendLine($"**Original:**");
        builder.AppendLine($"`{result.OriginalUrl}`");

        string failReason;
        switch (result.Result.FailValue)
        {
            case DownloadFailReason.VideoFetchError:
                failReason = "Video does not exist";
                break;
            default:
                failReason = "Download error";
                break;
        }
        builder.AppendLine($"### FAILED - _{failReason}_");
        
        return builder.ToString();
    }

    public static string GetLinkMessageString(LinksSchema.Entry entry, LinkMetadataSchema.Entry metadata)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"## {entry.Name}");
        
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

        builder.AppendLine($"```{Helpers.GetServerUrl(entry.UserId, entry.VideoId)}```");
        
        if (entry.DeleteAfter != DateTime.MaxValue)
        {
            builder.AppendLine($"Expires: <t:{(int)(entry.DeleteAfter - DateTime.UnixEpoch).TotalSeconds}:f>");
        }
        
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
        while (!AsyncQueues.DownloadResults.IsEmpty)
        {
            AsyncQueues.DownloadResults.TryDequeue(out DownloadResult downloadResult);
            await UpdateResultMessage(client, downloadResult);
        }
    }

    private static async Task UpdateResultMessage(DiscordClient client, DownloadResult downloadResult)
    {
        DiscordUser user = await client.GetUserAsync(downloadResult.UserId);
        DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
        DiscordMessage message = await dmChannelAsync.GetMessageAsync(downloadResult.MessageId);
        
        if (!downloadResult.Result.Success)
        {
            // Have different versions depending on type
            string failMessageString = GetLinkFailedMessage(downloadResult);
            await message.ModifyAsync(failMessageString);
            
            
        }
        else
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
            
            string messageString = GetLinkMessageString(entry.Value, metadata.Value);
            
            await message.ModifyAsync(messageString);

            await message.CreateReactionAsync(Constants.EmojiToCheck);
        }
    }

    public static async Task CheckForRemoveMessages(DiscordClient client)
    {
        while (!AsyncQueues.RemoveMessageQueue.IsEmpty)
        {
            AsyncQueues.RemoveMessageQueue.TryDequeue(out RemoveMessagePayload removedMessage);
            
            DiscordUser user = await client.GetUserAsync(removedMessage.UserId);
            DiscordDmChannel dmChannelAsync = await user.CreateDmChannelAsync();
            DiscordMessage message = await dmChannelAsync.GetMessageAsync(removedMessage.MessageId);

            await message.DeleteAsync();
        }
    }
}