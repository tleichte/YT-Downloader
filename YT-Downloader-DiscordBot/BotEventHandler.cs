using Common;
using Common.DatabaseSchemas;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace YT_Downloader_DiscordBot;

public class BotEventHandler : IEventHandler<MessageReactionAddedEventArgs>, IEventHandler<ModalSubmittedEventArgs>
{
    public async Task HandleEventAsync(DiscordClient sender, MessageReactionAddedEventArgs eventArgs)
    {
        // TODO
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
        QueueSchema.Entry? nameQueueEntry = await Database.Instance.QueueSchema.GetEntryForName(userId, name);
        if (nameQueueEntry != null)
        {
            DiscordDmChannel channel = await eventArgs.Interaction.User.CreateDmChannelAsync();
            DiscordMessage existingMessage = await channel.GetMessageAsync(nameQueueEntry.Value.MessageId);
            var response = $"You already have a video queued with the name {name}!\n{existingMessage.JumpLink}";
            await message.ModifyAsync(response);
            return;
        }
        
        // Check if name already exists in links
        LinksSchema.Entry? nameLinksEntry = await Database.Instance.LinksSchema.GetEntryForName(userId, name);
        if (nameLinksEntry != null)
        {
            DiscordDmChannel channel = await eventArgs.Interaction.User.CreateDmChannelAsync();
            DiscordMessage existingMessage = await channel.GetMessageAsync(nameLinksEntry.Value.MessageId);
            var response = $"You already have a video with the name {name}!\n{existingMessage.JumpLink}";
            await message.ModifyAsync(response);
            return;
        }

        Database.Instance.Mutex.WaitOne();

        await Database.Instance.QueueSchema.AddEntry(new QueueSchema.Entry
        {
            UserId = userId,
            Name = name,
            Url = url,
            MessageId = message.Id,
            Persistent = persistent,
            Status = QueueStatus.Queued,
            Progress = ""
        });
        
        Database.Instance.Mutex.ReleaseMutex();
        
        // This will send to queue
        var successResponse = $"Status: Added `{url}` to queue.";
        await message.ModifyAsync(successResponse);
    }
}