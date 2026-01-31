using Common;
using Common.DatabaseSchemas;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Newtonsoft.Json;

namespace YT_Downloader_DiscordBot;

public class SlashCommands : ApplicationCommandModule
{

    private static async void Respond(InteractionContext ctx)
    {
        // if (ctx.GetOriginalResponseAsync())
    }

    [SlashCommand("save", "Requests a link to be saved to the ")]
    public async Task SaveCommand(InteractionContext ctx, string name, string url, bool persistent)
    {
        await ctx.DeferAsync();

        ulong userId = ctx.User.Id;
        var guid = Guid.NewGuid();
        
        // Check if name already exists in queue
        QueueSchema.Entry? nameQueueEntry = await Database.Instance.QueueSchema.GetEntryForName(userId, name);
        if (nameQueueEntry != null)
        {
            var response = $"You already have a video queued with the name {name}!\n```\n{nameQueueEntry.Value.Url}```";
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(response));
            return;
        }
        
        // Check if name already exists in links
        LinksSchema.Entry? nameLinksEntry = await Database.Instance.LinksSchema.GetEntryForName(userId, name);
        if (nameLinksEntry != null)
        {
            var response = $"You already have a video with the name {name}!\n```\n{nameLinksEntry.Value}```";
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(response));
            return;
        }
        
        // Send queue entry to worker
        QueueSchema.Entry entry = new()
        {
            UserId = userId,
            LinkId = guid,
            Name = name,
            Url = url,
            Persistent = persistent,
            Status = QueueStatus.Queued
        };
        Result<AddToQueueFailReason> result = await BotHelpers.SendQueueEntry(entry);

        if (result.Success)
        {
            var response = $"Successfully added video to queue! You can check the status with `/status {name}`";
            DiscordMessage? message = await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));
            if (message != null)
            {
                
            }
        }
        else
        {
            var response = $"Something went wrong while adding to queue! (Reason: {result.FailValue.ToString()})";
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));
        }
    }

    [SlashCommand("status", "Check the status of a given name")]
    public async Task StatusCommand(InteractionContext ctx, string name)
    {
        // Check queries db
        ulong userId = ctx.User.Id;
        await Database.Instance.QueueSchema.GetEntryForName(userId, name);
        
        // TODO respond accordingly
    }

    [SlashCommand("get", "Gets the link for a given name. Will return multiple if query is similar")]
    public async Task GetCommand(InteractionContext ctx, string name)
    {
        
    }


    [SlashCommand("bedtime", "[ADMIN] Shuts down the bot")]
    public async Task BedtimeCommand(InteractionContext ctx)
    {
        if (ctx.User.Id != Config.Current.BotOwnerId)
        {
            await ctx.CreateResponseAsync("You're not the boss of me!");
            return;
        }
        
        await ctx.CreateResponseAsync(":wave:");
        ctx.Client.Dispose();
        Environment.Exit(0);
    }
    
}