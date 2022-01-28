﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Lilia.Commons;
using Lilia.Database;
using Lilia.Database.Extensions;
using Lilia.Database.Models;
using Lilia.Services;

namespace Lilia.Modules;

[SlashCommandGroup("queue", "Commands for music queue manipulation")]
public class MusicQueueModule : ApplicationCommandModule
{
    private LiliaDbContext _dbCtx;
    private LiliaClient _client;

    public MusicQueueModule(LiliaClient client)
    {
        this._client = client;
        this._dbCtx = client.Database.GetContext();
    }

    [SlashCommand("add", "Add a song to queue")]
    public async Task AddToQueueCommand(InteractionContext ctx,
        [Option("input", "Search input")] string search,
        [Choice("Youtube", "Youtube")]
        [Choice("SoundCloud", "SoundCloud")]
        [Option("mode", "Determine place to search")]
        string source = "Youtube")
    {
        await ctx.DeferAsync();

        LavalinkExtension lavalinkExtension = ctx.Client.GetLavalink();
        LavalinkNodeConnection node = lavalinkExtension.ConnectedNodes.Values.First();
        LavalinkLoadResult loadResult = await node.Rest.GetTracksAsync(search, Enum.Parse<LavalinkSearchType>(source));

        if (!loadResult.Tracks.Any())
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("I can't to find suitable tracks from your search"));

            return;
        }

        int index = 0;

        StringBuilder tracksStr = new();
        List<string> tracks = new();
        List<string> trackNames = new();

        foreach (var lavalinkTrack in loadResult.Tracks.Take(10))
        {
            // ignore livestream, radio, etc.
            if (lavalinkTrack.IsStream) continue;
            
            ++index;
            string wot = $"{Formatter.Bold(lavalinkTrack.Title)} by {Formatter.Bold(lavalinkTrack.Author)}";
            tracksStr.AppendLine($"Track #{index}: {Formatter.MaskedUrl(wot, lavalinkTrack.Uri)}");
            tracks.Add(lavalinkTrack.Uri.ToString());
            trackNames.Add(wot);
        }

        if (string.IsNullOrWhiteSpace(tracksStr.ToString()))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Nothing found. Maybe you gave me the radio link and I ignored it."));

            return;
        }

        DiscordEmbedBuilder embedBuilder = LiliaUtilities.GetDefaultEmbedTemplate(ctx.Member)
            .WithTitle($"Found {index} tracks with query {search} from {source}")
            .AddField("Results", tracksStr.ToString());

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Type the position of the track you want to get (0 to {index})\nOr type \"cancel\" to cancel")
            .AddEmbed(embedBuilder.Build()));
        
        int i = 1;

        InteractivityExtension interactivity = ctx.Client.GetInteractivity();
        
        var res = await interactivity.WaitForMessageAsync(x =>
        {
            bool canConvert = int.TryParse(x.Content, out i);
            return (x.Content.ToLower() == "cancel") || (canConvert && 1 <= i && i <= index);
        });

        if (res.TimedOut)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Timed out"));

            return;
        }

        DbGuild guild = this._dbCtx.GetOrCreateGuildRecord(ctx.Guild);

        guild.Queue += $"{tracks[i - 1]}||";
        guild.QueueWithNames += $"{trackNames[i - 1]}||";

        await this._dbCtx.SaveChangesAsync();

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Added track selection #{i} to queue."));
    }

    [SlashCommand("addplaylist", "Add songs in a playlist into queue")]
    public async Task AddPlaylistToQueueCommand(InteractionContext ctx,
        [Option("source", "Playlist URL")] string source)
    {
        await ctx.DeferAsync();

        LavalinkExtension lavalinkExtension = ctx.Client.GetLavalink();
        LavalinkNodeConnection node = lavalinkExtension.ConnectedNodes.Values.First();
        LavalinkLoadResult loadResult = await node.Rest.GetTracksAsync(source, LavalinkSearchType.Plain);

        if (!loadResult.Tracks.Any())
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("I am unable to find tracks from your playlist URL"));

            return;
        }

        DbGuild guild = this._dbCtx.GetOrCreateGuildRecord(ctx.Guild);

        StringBuilder tracksStr = new();
        StringBuilder queueApp = new();
        StringBuilder queueNameApp = new();

        foreach (var lavalinkTrack in loadResult.Tracks)
        {
            // don't you dare
            if (lavalinkTrack.IsStream) continue;
            
            string wot = $"{Formatter.Bold(lavalinkTrack.Title)} by {Formatter.Bold(lavalinkTrack.Author)}";
            tracksStr.AppendLine(wot);

            queueApp.Append($"{lavalinkTrack.Uri}||");
            queueNameApp.Append($"{wot}||");
        }
        
        if (string.IsNullOrWhiteSpace(tracksStr.ToString()))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Nothing found. Maybe you gave me the radio link and I ignored it."));

            return;
        }
        
        DiscordEmbedBuilder embedBuilder = LiliaUtilities.GetDefaultEmbedTemplate(ctx.Member)
            .WithTitle($"Found and added {loadResult.Tracks.Count()} tracks from provided playlist URL");
        
        guild.Queue += queueApp.ToString();
        guild.QueueWithNames += queueNameApp.ToString();
        await this._dbCtx.SaveChangesAsync();

        InteractivityExtension interactivity = ctx.Client.GetInteractivity();
        var p = interactivity.GeneratePagesInEmbed(tracksStr.ToString(), DSharpPlus.Interactivity.Enums.SplitType.Line, embedBuilder);

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent("If you see \"This interaction failed\", just ignore it. I can not find a workaround :D"));

        await ctx.Channel.SendPaginatedMessageAsync(ctx.Member, p, TimeSpan.FromMinutes(5), DSharpPlus.Interactivity.Enums.PaginationBehaviour.WrapAround);
    }

    [SlashCommand("view", "View this server's queue")]
    public async Task ViewQueueCommand(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        DbGuild guild = this._dbCtx.GetOrCreateGuildRecord(ctx.Guild);
        
        List<string> tracks = string.IsNullOrWhiteSpace(guild.QueueWithNames)
            ? new()
            : guild.QueueWithNames.Split("||").ToList();

        if (tracks.Any())
        {
            StringBuilder names = new();

            int pos = 0;
            foreach (string track in tracks)
            {
                if (string.IsNullOrWhiteSpace(track)) continue;
                ++pos;
                names.AppendLine($"Track #{pos}: {track}");
            }

            if (string.IsNullOrWhiteSpace(names.ToString()))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Something wrong happened"));    
            }

            DiscordEmbedBuilder embedBuilder = LiliaUtilities.GetDefaultEmbedTemplate(ctx.Member)
                .WithTitle($"Queue of guild \"{ctx.Guild.Name}\" ({tracks.Count - 1} tracks)");

            InteractivityExtension interactivity = ctx.Client.GetInteractivity();

            var p = interactivity.GeneratePagesInEmbed(names.ToString(), DSharpPlus.Interactivity.Enums.SplitType.Line, embedBuilder);

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("If you see \"This interaction failed\", just ignore it. I can not find a workaround :D"));

            await ctx.Channel.SendPaginatedMessageAsync(ctx.Member, p, TimeSpan.FromMinutes(5), DSharpPlus.Interactivity.Enums.PaginationBehaviour.WrapAround);
        }
        else
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("The queue is empty"));
        }
    }

    [SlashCommand("remove", "Remove a track from the queue")]
    public async Task RemoveFromQueueCommand(InteractionContext ctx,
        [Option("position", "Position to delete. Use \"/queue view\" to check")] long position)
    {
        await ctx.DeferAsync();

        DbGuild guild = this._dbCtx.GetOrCreateGuildRecord(ctx.Guild);

        List<string> tracks = string.IsNullOrWhiteSpace(guild.Queue)
            ? new()
            : guild.Queue.Split("||").ToList();

        List<string> trackNames = string.IsNullOrWhiteSpace(guild.QueueWithNames)
            ? new()
            : guild.QueueWithNames.Split("||").ToList();

        if (tracks.Any() && trackNames.Any())
        {
            int pos = (int) position;

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .WithContent($"Deleting track #{position} -> {trackNames[pos - 1]}"));

            tracks.RemoveAt(pos - 1);
            trackNames.RemoveAt(pos - 1);

            guild.Queue = string.Join("||", tracks);
            guild.QueueWithNames = string.Join("||", trackNames);

            await this._dbCtx.SaveChangesAsync();

            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Done deleting track #{position}"));
        }
        else
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("The queue is empty"));
        }
    }

    [SlashCommand("clear", "Clear entire queue")]
    [SlashRequirePermissions(Permissions.ManageGuild)]
    public async Task ClearQueueCommand(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        DbGuild guild = this._dbCtx.GetOrCreateGuildRecord(ctx.Guild);

        guild.Queue = string.Empty;
        guild.QueueWithNames = string.Empty;

        await this._dbCtx.SaveChangesAsync();

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent("Done clearing the queue"));
    }
}