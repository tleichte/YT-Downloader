using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using YT_Downloader_DiscordBot.DB;
using YT_Downloader_DiscordBot.DB.Schemas;
using YT_Downloader_DiscordBot.Helper;

namespace YT_Downloader_DiscordBot;

public class BotEventHandler : IEventHandler<MessageReactionAddedEventArgs>, IEventHandler<ModalSubmittedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs eventArgs)
    {
        if (eventArgs.User.Id == sender.CurrentUser.Id)
        {
            return;
        }

        if (!eventArgs.Emoji.Equals(Constants.EmojiToCheck))
        {
            if (!eventArgs.Emoji.Equals(Constants.SafetyDeleteEmoji))
            {
                return;
            }

            Database.Current.Mutex.WaitOne();
            LinksSchema.Entry? linksCheck = await Database.Current.LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
            QueueSchema.Entry? queueCheck = await Database.Current.QueueSchema.GetEntryForMessageId(eventArgs.Message.Id);
            Database.Current.Mutex.ReleaseMutex();
            if (linksCheck != null || queueCheck != null)
            {
                return;
            }
            
            try
            {
                await eventArgs.Message.DeleteAsync();
            }
            catch (Exception)
            {
                // ignored
            }
            return;
        }

        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? value = await Database.Current.LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
        Database.Current.Mutex.ReleaseMutex();
        
        if (value == null)
        {
            return;
        }

        await ActionsHelper.DeleteLink(value.Value);

        await eventArgs.Message.DeleteAsync();
        
        await Task.Yield();
    }

    private static bool TryGetModalTextValue(string key, ModalSubmittedEventArgs eventArgs, out string? value)
    {
        if (eventArgs.Values.TryGetValue(key, out IModalSubmission? urlSubmission))
        {
            if (urlSubmission is TextInputModalSubmission textSubmission)
            {
                value = textSubmission.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetModalSelectValues(string key, ModalSubmittedEventArgs eventArgs, out IReadOnlyList<string>? value)
    {
        if (eventArgs.Values.TryGetValue(key, out IModalSubmission? urlSubmission))
        {
            if (urlSubmission is SelectMenuModalSubmission selectSubmission)
            {
                value = selectSubmission.Values;
                return true;
            }
        }
        value = null;
        return false;
    }
    
    public async Task HandleEventAsync(DiscordClient sender, ModalSubmittedEventArgs eventArgs)
    {

        await eventArgs.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
        
        if (!TryGetModalTextValue(Constants.ModalURLId, eventArgs, out string? urlModalValue))
        {
            return;
        }
        
        if (!TryGetModalTextValue(Constants.ModalNameId, eventArgs, out string? nameModalValue))
        {
            return;
        }

        if (!TryGetModalSelectValues(Constants.ModalExpiresId, eventArgs, out IReadOnlyList<string>? expiresModalValue))
        {
            return;
        }
        
        string url = urlModalValue ?? "";
        string name = nameModalValue ?? "";
        bool persistent = expiresModalValue?.Count == 1 && expiresModalValue[0] == Constants.ModalExpireOptionNeverId;
        
        string preQueueMessage = $"Adding `{url}` to queue...";
        DiscordMessage message = await eventArgs.Interaction.User.SendMessageAsync(preQueueMessage);
        
        // Check if valid URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            var response = $"`{url}` is not a valid URL!";
            await message.ModifyAsync(response);
            BotHelpers.RunDeleteAfter(eventArgs.Interaction.User.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
        }
        
        ulong userId = eventArgs.Interaction.User.Id;
        
        // Check if name already exists in queue
        Database.Current.Mutex.WaitOne();
        QueueSchema.Entry? nameQueueEntry = await Database.Current.QueueSchema.GetEntryForName(userId, name);
        Database.Current.Mutex.ReleaseMutex();
        if (nameQueueEntry != null)
        {
            DiscordDmChannel channel = await eventArgs.Interaction.User.CreateDmChannelAsync();
            DiscordMessage existingMessage = await channel.GetMessageAsync(nameQueueEntry.Value.MessageId);
            var response = $"You already have a video queued with the name {name}!\n{existingMessage.JumpLink}";
            await message.ModifyAsync(response);
            BotHelpers.RunDeleteAfter(eventArgs.Interaction.User.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
            return;
        }
        
        // Check if name already exists in links
        Database.Current.Mutex.WaitOne();
        LinksSchema.Entry? nameLinksEntry = await Database.Current.LinksSchema.GetEntryForName(userId, name);
        Database.Current.Mutex.ReleaseMutex();
        if (nameLinksEntry != null)
        {
            DiscordDmChannel channel = await eventArgs.Interaction.User.CreateDmChannelAsync();
            DiscordMessage existingMessage = await channel.GetMessageAsync(nameLinksEntry.Value.MessageId);
            var response = $"You already have a video with the name {name}!\n{existingMessage.JumpLink}";
            await message.ModifyAsync(response);
            BotHelpers.RunDeleteAfter(eventArgs.Interaction.User.Id, message.Id, Constants.ErrorMessageTimeoutMinutes);
            return;
        }

        Database.Current.Mutex.WaitOne();

        await Database.Current.QueueSchema.AddEntry(new QueueSchema.Entry
        {
            UserId = userId,
            Name = name,
            Url = url,
            MessageId = message.Id,
            Persistent = persistent,
            Status = QueueStatus.Queued,
            Progress = ""
        });
        
        Database.Current.Mutex.ReleaseMutex();
        
        var builder = new StringBuilder();
        
        builder.AppendLine($"## {name}");
        builder.AppendLine("**Original:**");
        builder.AppendLine($"`{url}`");
        builder.AppendLine();
        builder.AppendLine("**Status:**");
        builder.AppendLine("_QUEUED_");
        
        await message.ModifyAsync(builder.ToString());
    }
}