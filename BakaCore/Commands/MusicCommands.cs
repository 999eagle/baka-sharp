using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


using Discord;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;
using Concentus.Structs;
using Concentus.Oggfile;
using YoutubeExplode;

using BakaCore.Music;
using BakaCore.Services;

namespace BakaCore.Commands
{
	class MusicCommands
	{
		private ILogger logger;
		private DiscordSocketClient client;
		private YouTubeService youtubeService;
		private PlayerService playerService;
		private MusicService musicService;

		public MusicCommands(IServiceProvider services)
		{
			var loggerFactory = services.GetRequiredService<ILoggerFactory>();
			logger = loggerFactory.CreateLogger<MusicCommands>();
			client = services.GetRequiredService<DiscordSocketClient>();
			youtubeService = services.GetRequiredService<YouTubeService>();
			musicService = services.GetRequiredService<MusicService>();
			playerService = services.GetRequiredService<PlayerService>();
		}

		private IVoiceChannel GetVoiceChannelFromMessage(SocketMessage message)
		{
			return (message.Author as IGuildUser)?.VoiceChannel;
		}

		private IGuild GetGuildFromMessage(SocketMessage message)
		{
			return (message.Channel as IGuildChannel)?.Guild;
		}

		private async Task<string> SearchYoutube(string searchText)
		{
			var request = youtubeService.Search.List("snippet");
			request.Q = searchText;
			request.MaxResults = 1;
			request.Type = "video";
			var response = await request.ExecuteAsync();
			return response.Items.FirstOrDefault()?.Id.VideoId;
		}

		[Command("play", Scope = CommandScope.Guild)]
		public async Task PlayCommand(SocketMessage message, [FullText] string text)
		{
			var channel = GetVoiceChannelFromMessage(message);
			var player = playerService.GetPlayerForGuild(GetGuildFromMessage(message));
			if (channel == null && player?.VoiceChannel == null)
			{
				await message.Channel.SendMessageAsync("Please join a voice channel first and try again");
				return;
			}

			text = text.Replace('`', '\'');
			if (YoutubeClient.TryParseVideoId(text, out var videoId))
			{
				// video id parsed successfully, nothing to do
			}
			else
			{
				await message.Channel.SendMessageAsync($"**Searching** :mag_right: `{text}`");
				videoId = await SearchYoutube(text);
				if (videoId == null)
				{
					await message.Channel.SendMessageAsync("Couldn't find anything for your search...");
					return;
				}
			}

			var detailRequest = youtubeService.Videos.List("contentDetails,snippet");
			detailRequest.Id = videoId;
			var detailResponse = await detailRequest.ExecuteAsync();
			if (detailResponse.Items.Count < 1)
			{
				await message.Channel.SendMessageAsync("I can't access that video...");
				return;
			}
			// using XmlConvert because that supports the ISO8601 format used in the response
			var duration = System.Xml.XmlConvert.ToTimeSpan(detailResponse.Items[0].ContentDetails.Duration);
			var embed = new EmbedBuilder()
				.WithAuthor("Added to queue")
				.WithTitle(detailResponse.Items[0].Snippet.Title)
				.WithUrl($"https://www.youtube.com/watch?v={videoId}")
				.AddField("Channel", detailResponse.Items[0].Snippet.ChannelTitle, true)
				.AddField("Length", duration.ToString(), true)
				.WithThumbnailUrl(detailResponse.Items[0].Snippet.Thumbnails.Default__.Url)
				.Build();
			await message.Channel.SendMessageAsync("", false, embed);

			var song = await musicService.DownloadFromYoutube(videoId);
			playerService.EnqueueAndPlay(channel, song.Id);
		}

		[Command("stop", Scope = CommandScope.Guild)]
		public async Task StopCommand(SocketMessage message)
		{
			await playerService.StopPlayerInGuild(GetGuildFromMessage(message));
		}
	}
}
