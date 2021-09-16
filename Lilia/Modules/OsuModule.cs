﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Lilia.Database;
using Lilia.Database.Models;
using Lilia.Services;
using OsuSharp;
using OsuSharp.Oppai;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nalia.Modules
{
	[Group("osu")]
	[Description("Represents for osu! commands (only works in Bancho server, other servers will be supported soon.")]
	public class OsuModule : BaseCommandModule
	{
		private LiliaClient _client;
		private LiliaDbContext _dbCtx;
		private OsuClient _osuApiClient;

		private DiscordColor OsuEmbedColor => DiscordColor.HotPink;

		public OsuModule(LiliaClient client)
		{
			this._client = client;
			this._dbCtx = client.Database.GetContext();
			this._osuApiClient = new OsuClient(new OsuSharpConfiguration
			{
				ApiKey = this._client.Configurations.Credentials.OsuApiKey,
				ModeSeparator = string.Empty
			});
		}

		[Command("set")]
		[Aliases("link")]
		[Description("Link your osu! profile data to my database for future searches.")]
		public async Task SetOsuProfileCommand(CommandContext ctx,
			[Description("Your osu! username, IN QUOTES.")] string username,
			[Description("Mode number: 0 - osu!, 1 - Taiko, 2 - osu!catch, 3 - osu!mania. Defaults to 0 (osu!).")] int mode = 0)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(ctx.Member.Id);

			user.OsuMode = mode;
			user.OsuUsername = username;

			this._dbCtx.Update(user);
			this._dbCtx.SaveChanges();
			await ctx.RespondAsync($"Successfully set your osu! username to {Formatter.Bold(username)} and your osu! mode to {Formatter.Bold(((GameMode)mode).ToString())}");
		}

		[Command("linked")]
		[Description("See your linked data with me.")]
		public async Task GetLinkedInfoCommand(CommandContext ctx)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(ctx.Member.Id);

			DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
				.AddField("Username", user.OsuUsername ?? "Not linked yet.", true)
				.AddField("Mode", ((GameMode)user.OsuMode).ToString(), true)
				.WithTimestamp(DateTime.Now)
				.WithFooter($"Requested by {ctx.User.Username}#{ctx.User.Discriminator}", ctx.User.AvatarUrl)
				.WithColor(this.OsuEmbedColor);

			await ctx.RespondAsync(embed: embedBuilder.Build());
		}

		#region "user"/"profile" command overloads
		[Command("user")]
		[Aliases("profile")]
		[Description("Get osu! detailed profile information.")]
		public async Task GetOsuUserCommand(CommandContext ctx)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(ctx.Member.Id);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("You have not linked your osu! account yet.");
			else
				await this.GetOsuUserCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("user")]
		public async Task GetOsuUserCommand(CommandContext ctx,
			[Description("Discord user mention to get data. Might be annoying.")] DiscordMember mentionedMember)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(mentionedMember.Id);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("That user has not linked their osu! account yet.");
			else
				await this.GetOsuUserCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("user")]
		public async Task GetOsuUserCommand(CommandContext ctx,
			[Description("Discord user ID to get data.")] ulong userId)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(userId);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("That user has not linked their osu! account yet.");
			else
				await this.GetOsuUserCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("user")]
		public async Task GetOsuUserCommand(CommandContext ctx,
			[Description("Username to get data, IN QUOTES.")] string username,
			[Description("Mode number: 0 - osu!, 1 - Taiko, 2 - osu!catch, 3 - osu!mania. Defaults to 0 (osu!).")] int mode = 0)
		{
			User user = await this._osuApiClient.GetUserByUsernameAsync(username.Replace("\"", ""), (GameMode)mode);

			if (user == null)
			{
				await ctx.RespondAsync("User not found.");
			}
			else
			{
				DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
					.WithTimestamp(DateTime.Now)
					.WithFooter($"Requested by {ctx.User.Username}#{ctx.User.Discriminator}", ctx.User.AvatarUrl)
					.WithAuthor(user.Username, $"https://osu.ppy.sh/users/{user.UserId}")
					.WithColor(this.OsuEmbedColor)
					.AddField(":information_source: Basic Information",
						$"**>Country** : :flag_{user.Country.Name.ToLower()}: {user.Country.EnglishName}\n" +
						$">**PP** : {Math.Round(user.PerformancePoints.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero)}pp ( :flag_{user.Country.Name.ToLower()}: : #{user.CountryRank} - :globe_with_meridians: : #{user.Rank})\n" +
						$">**Level** : {Convert.ToInt32(user.Level)}\n" +
						$">**Play Count** : {user.PlayCount}\n" +
						$">**Accuracy** : {Math.Round(user.Accuracy.GetValueOrDefault(), 2, MidpointRounding.AwayFromZero)}%")
					.WithThumbnail($"https://a.ppy.sh/{user.UserId}");

				await ctx.RespondAsync(embed: embedBuilder.Build());
			}
		}
		#endregion

		#region "recent"/"r" command overloads
		[Command("recent")]
		[Aliases("r")]
		[Description("Get most recent score of an user.")]
		public async Task GetRecentScoreCommand(CommandContext ctx)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(ctx.Member.Id);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("You have not linked your osu! account yet.");
			else
				await this.GetRecentScoreCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("recent")]
		public async Task GetRecentScoreCommand(CommandContext ctx,
			[Description("Discord user mention to get data. Might be annoying.")] DiscordMember mentionedMember)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(mentionedMember.Id);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("That user has not linked their osu! account yet.");
			else
				await this.GetRecentScoreCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("recent")]
		public async Task GetRecentScoreCommand(CommandContext ctx,
			[Description("Discord user ID to get data.")] ulong userId)
		{
			DbUser user = this._dbCtx.GetOrCreateUserRecord(userId);

			if (string.IsNullOrWhiteSpace(user.OsuUsername))
				await ctx.RespondAsync("That user has not linked their osu! account yet.");
			else
				await this.GetRecentScoreCommand(ctx, user.OsuUsername, user.OsuMode);
		}

		[Command("recent")]
		public async Task GetRecentScoreCommand(CommandContext ctx,
			[Description("Username to get data, IN QUOTES.")] string username,
			[Description("Mode to get data. Default to 0 (std).")] int mode = 0)
		{
			User user = await this._osuApiClient.GetUserByUsernameAsync(username, (GameMode)mode);

			if (user == null)
				await ctx.RespondAsync("User not found.");
			else
			{
				Score recentScore = (await this._osuApiClient.GetUserRecentsByUsernameAsync(username, (GameMode)mode, 1)).FirstOrDefault();

				if (recentScore == null)
					await ctx.RespondAsync("This user have not played anything recently.");
				else
				{
					Beatmap beatmap = await this._osuApiClient.GetBeatmapByIdAsync(recentScore.BeatmapId);

					PerformanceData fc = await beatmap.GetPPAsync(recentScore.Mods, (float)user.Accuracy.GetValueOrDefault());
					PerformanceData currentProgress = await recentScore.GetPPAsync();

					DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
						.WithTimestamp(DateTime.Now)
						.WithFooter($"Requested by {ctx.User.Username}#{ctx.User.Discriminator}", ctx.User.AvatarUrl)
						.WithAuthor($"Recent play of {user.Username} in mode {(GameMode)mode}",
							$"https://osu.ppy.sh/users/{user.UserId}",
							$"https://a.ppy.sh/{user.UserId}")
						.WithColor(this.OsuEmbedColor)
						.AddField(
							Formatter.MaskedUrl(
								$"{beatmap.Artist} - {beatmap.Title} [{beatmap.Difficulty}] **+{recentScore.Mods.ToModeString(this._osuApiClient)}**",
								beatmap.BeatmapUri),
							$">**Ranking** : {recentScore.Rank}\n" +
							$">**Accuracy** : {Math.Round(recentScore.Accuracy, 2, MidpointRounding.AwayFromZero)}%\n" +
							$">**Max Combo** : {recentScore.MaxCombo}x/{beatmap.MaxCombo}x\n" +
							$">**Hit Count** : [{recentScore.Count300}/{recentScore.Count100}/{recentScore.Count50}/{recentScore.Miss}]\n" +
							$">**PP** : {Math.Round(currentProgress.Pp, 2, MidpointRounding.AwayFromZero)}pp - **{Math.Round(fc.Pp, 2, MidpointRounding.AwayFromZero)}pp** for {Math.Round(recentScore.Accuracy)}% FC")
						.WithThumbnail(beatmap.CoverUri);

					await ctx.RespondAsync(embed: embedBuilder.Build());
				}
			}
		}
		#endregion

		#region "best"/"top" command overloads
		[Command("best")]
		[Aliases("top")]
		[Description("Get best record(s) of an user.")]
		public async Task GetBestRecordsCommand(CommandContext ctx,
			[Description("Number of best records to get data. Defaults to 1.")] int amount = 1)
		{
			DbUser dbUser = this._dbCtx.GetOrCreateUserRecord(ctx.Member.Id);

			if (string.IsNullOrWhiteSpace(dbUser.OsuUsername))
				await ctx.RespondAsync("You have not linked your osu! account yet.");
			else
				await this.GetBestRecordsCommand(ctx, dbUser.OsuUsername, amount, dbUser.OsuMode);
		}

		[Command("best")]
		public async Task GetBestRecordsCommand(CommandContext ctx,
			[Description("Discord user mention to get data. Might be annoying.")] DiscordMember mentionedMember,
			[Description("Number of best records to get data. Defaults to 1.")] int amount = 1)
		{
			DbUser dbUser = this._dbCtx.GetOrCreateUserRecord(mentionedMember.Id);

			if (string.IsNullOrWhiteSpace(dbUser.OsuUsername))
				await ctx.RespondAsync("You have not linked your osu! account yet.");
			else
				await this.GetBestRecordsCommand(ctx, dbUser.OsuUsername, amount, dbUser.OsuMode);
		}

		[Command("best")]
		public async Task GetBestRecordsCommand(CommandContext ctx,
			[Description("Discord user ID to get data.")] ulong userId,
			[Description("Number of best records to get data. Defaults to 1.")] int amount = 1)
        {
			DbUser dbUser = this._dbCtx.GetOrCreateUserRecord(userId);

			if (string.IsNullOrWhiteSpace(dbUser.OsuUsername))
				await ctx.RespondAsync("That user has not linked their osu! account yet.");
			else
				await this.GetBestRecordsCommand(ctx, dbUser.OsuUsername, amount, dbUser.OsuMode);
		}

		[Command("best")]
		public async Task GetBestRecordsCommand(CommandContext ctx,
			[Description("Username to get best records, IN QUOTES.")] string username,
			[Description("Mode to get best records. Default to 0 (std)")] int mode = 0,
			[Description("Number of best records to get data. Defaults to 1.")] int amount = 1)
		{
			User user = await this._osuApiClient.GetUserByUsernameAsync(username, (GameMode)mode);

			if (user == null)
				await ctx.RespondAsync("User not found.");
			else
			{
				IReadOnlyList<Score> scores = await this._osuApiClient.GetUserBestsByUsernameAsync(username, (GameMode) mode, amount);

				if (!scores.Any())
					await ctx.RespondAsync("This user have not played something yet.");
				else
				{
					foreach (Score score in scores)
					{
						Beatmap beatmap = await this._osuApiClient.GetBeatmapByIdAsync(score.BeatmapId);

						PerformanceData fc = await beatmap.GetPPAsync(score.Mods, (float)user.Accuracy.GetValueOrDefault());
						PerformanceData currentProgress = await score.GetPPAsync();

						DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
							.WithTimestamp(DateTime.Now)
							.WithFooter($"Requested by {ctx.User.Username}#{ctx.User.Discriminator}", ctx.User.AvatarUrl)
							.WithAuthor($"Best play of {user.Username} in mode {(GameMode)mode}", $"https://osu.ppy.sh/users/{user.UserId}",
								$"https://a.ppy.sh/{user.UserId}")
							.WithColor(this.OsuEmbedColor)
							.AddField(
								Formatter.MaskedUrl($"{beatmap.Artist} - {beatmap.Title} [{beatmap.Difficulty}] **+{score.Mods.ToModeString(this._osuApiClient)}**", beatmap.BeatmapUri),
								$">**Ranking** : {score.Rank}\n" +
								$">**Accuracy** : {Math.Round(score.Accuracy, 2, MidpointRounding.AwayFromZero)}%\n" +
								$">**Max Combo** : {score.MaxCombo}x/{beatmap.MaxCombo}x\n" +
								$">**Hit Count** : [{score.Count300}/{score.Count100}/{score.Count50}/{score.Miss}]\n" +
								$">**PP** : {Math.Round(currentProgress.Pp, 2, MidpointRounding.AwayFromZero)}pp - **{Math.Round(fc.Pp, 2, MidpointRounding.AwayFromZero)}pp** for {Math.Round(score.Accuracy)}% FC")
							.WithThumbnail(beatmap.CoverUri);

						await ctx.RespondAsync(embed: embedBuilder.Build());
					}
				}
			}
		}
		#endregion
	}
}
