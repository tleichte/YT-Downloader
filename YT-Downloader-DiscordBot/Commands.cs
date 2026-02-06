using System.ComponentModel;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.Metadata;
using DSharpPlus.Entities;
using YT_Downloader_DiscordBot.Pipes;

namespace YT_Downloader_DiscordBot;

public class Commands
{
    [Command("save")]
    [Description("Saves a link to the server.")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM)]
    public async Task SaveCommand(SlashCommandContext ctx, string name, string url, bool persistent = false)
    {
        await ctx.DeferResponseAsync(true);

        string preQueueMessage = $"Adding `{url}` to queue...";
        DiscordMessage message = await ctx.User.SendMessageAsync(preQueueMessage);
        
        ulong userId = ctx.User.Id;
        
        // Check if name already exists in queue
        QueueSchema.Entry? nameQueueEntry = await Database.Instance.QueueSchema.GetEntryForName(userId, name);
        if (nameQueueEntry != null)
        {
            var response = $"You already have a video queued with the name {name}!\n```\n{nameQueueEntry.Value.Url}```";
            await message.ModifyAsync(response);
            return;
        }
        
        // Check if name already exists in links
        LinksSchema.Entry? nameLinksEntry = await Database.Instance.LinksSchema.GetEntryForName(userId, name);
        if (nameLinksEntry != null)
        {
            var response = $"You already have a video with the name {name}!\n```\n{nameLinksEntry.Value}```";
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
        
        await ctx.DeleteResponseAsync();
    }

    [Command("bedtime")]
    [Description("[ADMIN] Shuts down the bot")]
    [InteractionAllowedContexts(DiscordInteractionContextType.BotDM)]
    public async Task BedtimeCommand(SlashCommandContext ctx)
    {
        if (ctx.User.Id != Config.Current.BotOwnerId)
        {
            await ctx.RespondAsync("You're not the boss of me!");
            return;
        }
        
        await ctx.RespondAsync(":wave:");
        ctx.Client.Dispose();
        Environment.Exit(0);
    }
    
}