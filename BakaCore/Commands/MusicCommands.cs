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
using YoutubeExplode.Exceptions;

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

		private async Task<Song> LoadSong(string searchText, ISocketMessageChannel feedbackChannel = null)
		{
			searchText = searchText.Replace('`', '\'');
			if (YoutubeClient.TryParseVideoId(searchText, out var videoId))
			{
				// video id parsed successfully, nothing to do
			}
			else
			{
				if (feedbackChannel != null) await feedbackChannel.SendMessageAsync($"**Searching** :mag_right: `{searchText}`");
				videoId = await SearchYoutube(searchText);
				if (videoId == null)
				{
					if (feedbackChannel != null) await feedbackChannel.SendMessageAsync("Couldn't find anything for your search...");
					return null;
				}
			}

			if (feedbackChannel != null)
			{
				var detailRequest = youtubeService.Videos.List("contentDetails,snippet");
				detailRequest.Id = videoId;
				var detailResponse = await detailRequest.ExecuteAsync();
				if (detailResponse.Items.Count < 1)
				{
					await feedbackChannel?.SendMessageAsync("I can't access that video...");
					return null;
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
				await feedbackChannel.SendMessageAsync("", false, embed);
			}
			try
			{
				return await musicService.DownloadFromYoutube(videoId);
			}
			catch(VideoUnavailableException)
			{
				if (feedbackChannel != null) await feedbackChannel.SendMessageAsync("I can't access that video...");
			}
			catch(VideoTooLongException ex)
			{
				if (feedbackChannel != null) await feedbackChannel.SendMessageAsync($"This video is too long. Please use only videos shorter than {ex.MaxLength:m\\:ss} minutes");
			}
			return null;
		}

		[Command("play", Scope = CommandScope.Guild)]
		public async Task PlayCommand(SocketMessage message, [FullText] string text)
		{
			var channel = GetVoiceChannelFromMessage(message);
			var player = playerService.GetPlayerForGuild(GetGuildFromMessage(message));
			if (channel == null && player?.VoiceChannel == null)
			{
				await message.Channel.SendMessageAsync("Please join a voice channel first and try again");
				await LoadSong(text); // start preloading song
				return;
			}
			var songTask = LoadSong(text, message.Channel)
				.ContinueWith((task, state) =>
				{
					var song = task.GetAwaiter().GetResult();
					if (song != null)
					{
						playerService.EnqueueAndPlay(channel, song.Id);
					}
				}, null);
			await Task.Delay(3000);
			player = playerService.GetPlayerForGuild(GetGuildFromMessage(message));
			if (!songTask.IsCompleted && (player == null || player.PlayerState == PlayerState.Disconnected))
			{
				await message.Channel.SendMessageAsync("Please wait a moment, the music will start soon");
			}
			await songTask;
		}

		[Command("stop", Scope = CommandScope.Guild)]
		public async Task StopCommand(SocketMessage message)
		{
			await playerService.StopPlayerInGuild(GetGuildFromMessage(message));
		}
	}
}
