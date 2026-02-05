using System.ComponentModel;
using Common;
using Common.DatabaseSchemas;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using YT_Downloader_DiscordBot.Pipes;

namespace YT_Downloader_DiscordBot;

public class Commands
{
    [Command("save")]
    [Description("Saves a link to the server.")]
    public async Task SaveCommand(SlashCommandContext ctx, string name, string url, bool persistent)
    {
        await ctx.DeferResponseAsync();

        ulong userId = ctx.User.Id;
        
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

        string preQueueMessage = $"Adding {url} to queue...";
        DiscordMessage message = await ctx.EditResponseAsync(preQueueMessage);
        
        try
        {
            // Send queue entry to worker
            QueueSchema.Entry entry = new()
            {
                UserId = userId,
                Name = name,
                Url = url,
                MessageId = message.Id,
                Persistent = persistent,
                Status = QueueStatus.Queued
            };
            
            Result<AddToQueueFailReason> result = await new QueuePipeClient().Send(entry);
            
            if (result.Success)
            {
                var response = $"Status: Added to queue.";
                await message.ModifyAsync(response);
            }
            else
            {
                var response = $"Something went wrong while adding to queue! (Reason: {result.FailValue.ToString()})";
                await ctx.EditResponseAsync(response);
            }
        }
        catch (Exception)
        {
            var response = "Something went wrong while adding to queue!";
            await ctx.EditResponseAsync(response);
        }
    }

    [Command("find")]
    [Description("Finds the link for a given name. Will return multiple if query is similar.")]
    public async Task FindCommand(SlashCommandContext ctx, string name)
    {
        await ctx.DeferResponseAsync(ephemeral:true);

        var response = "";
        
        await ctx.EditResponseAsync(response);
    }


    [Command("bedtime")]
    [Description("[ADMIN] Shuts down the bot")]
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