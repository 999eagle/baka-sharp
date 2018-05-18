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

using BakaCore.Music;

namespace BakaCore.Commands
{
	class MusicCommands
	{
		private ILogger logger;
		private ILoggerFactory loggerFactory;
		private DiscordSocketClient client;
		private YouTubeService youtubeService;
		private IDictionary<ulong, Player> players = new Dictionary<ulong, Player>();

		public MusicCommands(IServiceProvider services)
		{
			loggerFactory = services.GetRequiredService<ILoggerFactory>();
			logger = loggerFactory.CreateLogger<MusicCommands>();
			client = services.GetRequiredService<DiscordSocketClient>();
			youtubeService = services.GetRequiredService<YouTubeService>();
		}

		private Player GetPlayer(SocketMessage senderMessage)
		{
			if (!(senderMessage.Channel is SocketGuildChannel channel))
			{
				return null;
			}
			if (!players.TryGetValue(channel.Guild.Id, out var player))
			{
				return null;
			}
			else
			{
				return player;
			}
		}

		private async Task<Player> GetOrCreatePlayer(SocketMessage senderMessage, IVoiceChannel newChannel = null)
		{
			newChannel = newChannel ?? (senderMessage.Author as IGuildUser)?.VoiceChannel;
			var player = GetPlayer(senderMessage);

			if (player == null)
			{
				if (newChannel == null)
				{
					await senderMessage.Channel.SendMessageAsync("You need to join a voice channel first.");
					return null;
				}
				player = new Player(newChannel, loggerFactory);
				players[player.Guild.Id] = player;
			}
			else
			{
				// TODO: leave channel and join new channel
			}
			return player;
		}

		[Command("play", Scope = CommandScope.Guild)]
		public async Task PlayCommand(SocketMessage message, [FullText] string text)
		{
			text = text.Replace('`', '\'');
			await message.Channel.SendMessageAsync($"**Searching** :mag_right: `{text}`");
			var request = youtubeService.Search.List("snippet");
			request.Q = text;
			request.MaxResults = 1;
			request.Type = "video";
			var response = await request.ExecuteAsync();
			var result = response.Items.FirstOrDefault();
			if (result == null)
			{
				await message.Channel.SendMessageAsync("Couldn't find anything for your search...");
				return;
			}

			var player = await GetOrCreatePlayer(message);
			if (player.PlayerState == PlayerState.Disconnected)
			{
				var playlist = new Playlist();
				playlist.songs.Add(result.Id.VideoId);
				player.Start(playlist);
			}
			else
			{
				// TODO: Enqueue song
			}

			var detailRequest = youtubeService.Videos.List("contentDetails");
			detailRequest.Id = result.Id.VideoId;
			var detailResponse = await detailRequest.ExecuteAsync();
			// using XmlConvert because that supports the ISO8601 format used in the response
			var duration = System.Xml.XmlConvert.ToTimeSpan(detailResponse.Items[0].ContentDetails.Duration);
			var embed = new EmbedBuilder()
				.WithAuthor("Added to queue")
				.WithTitle(result.Snippet.Title)
				.WithUrl($"https://www.youtube.com/watch?v={result.Id.VideoId}")
				.AddField("Channel", result.Snippet.ChannelTitle, true)
				.AddField("Length", duration.ToString(), true)
				.WithThumbnailUrl(result.Snippet.Thumbnails.Default__.Url)
				.Build();
			await message.Channel.SendMessageAsync("", false, embed);
		}

		[Command("stop", Scope = CommandScope.Guild)]
		public async Task StopCommand(SocketMessage message)
		{
			var player = GetPlayer(message);
			if (player == null || player.PlayerState == PlayerState.Disconnected) return;
			await player.Stop();
		}
	}
}
