using System.Text;
using Common;
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
            return;
        }

        LinksSchema.Entry? value = await Database.Current.LinksSchema.GetEntryForMessageId(eventArgs.Message.Id);
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
        
        ulong userId = eventArgs.Interaction.User.Id;
        
        // Check if name already exists in queue
        QueueSchema.Entry? nameQueueEntry = await Database.Current.QueueSchema.GetEntryForName(userId, name);
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
        LinksSchema.Entry? nameLinksEntry = await Database.Current.LinksSchema.GetEntryForName(userId, name);
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