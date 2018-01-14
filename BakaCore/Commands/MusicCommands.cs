using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;


using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;

namespace BakaCore.Commands
{
	class MusicCommands
	{
		class GuildState
		{
			public IGuild guild;
			public IAudioClient client;
			public IAudioChannel channel;
		}

		private DiscordSocketClient client;
		private YouTubeService youtubeService;
		private IDictionary<ulong, GuildState> guildData = new Dictionary<ulong, GuildState>();

		public MusicCommands(IServiceProvider services)
		{
			client = services.GetRequiredService<DiscordSocketClient>();
			youtubeService = services.GetRequiredService<YouTubeService>();
		}

		private GuildState GetGuildState(SocketMessage senderMessage)
		{
			if (!(senderMessage.Channel is SocketGuildChannel channel))
			{
				return null;
			}
			if (!guildData.TryGetValue(channel.Guild.Id, out var state))
			{
				return null;
			}
			else
			{
				return state;
			}
		}

		private async Task<GuildState> StartOrGetAudioClient(SocketMessage senderMessage)
		{
			var state = GetGuildState(senderMessage);
			if (state != null) return state;
			if (!(senderMessage.Channel is SocketGuildChannel guildChannel))
			{
				await senderMessage.Channel.SendMessageAsync("Music can only be played on servers, sorry.");
				return null;
			}
			var newAudioChannel = guildChannel.Guild.Channels.FirstOrDefault(c => c is IAudioChannel && c.Users.Any(u => u.Id == senderMessage.Author.Id)) as IAudioChannel;
			if (newAudioChannel == null)
			{
				await senderMessage.Channel.SendMessageAsync("Please join a voice channel first.");
				return null;
			}
			guildData[guildChannel.Guild.Id] = state = new GuildState { guild = guildChannel.Guild, channel = newAudioChannel };
			guildData[guildChannel.Guild.Id].client = await newAudioChannel.ConnectAsync();
			return state;
		}

		[Command("play", Scope = CommandScope.Guild)]
		public async Task PlayCommand(SocketMessage message, [FullText] string text)
		{
			var state = await StartOrGetAudioClient(message);
			if (state == null) return;
			while (state.client == null) await Task.Delay(100);

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
				.AddField("Length", duration.ToString("hh:mm:ss"), true)
				.WithThumbnailUrl(result.Snippet.Thumbnails.Default__.Url)
				.Build();
			await message.Channel.SendMessageAsync("", false, embed);
		}

		[Command("stop", Scope = CommandScope.Guild)]
		public async Task StopCommand(SocketMessage message)
		{
			var state = GetGuildState(message);
			if (state == null || state.client == null) { return; }
			await state.client.StopAsync();
			guildData.Remove(state.guild.Id);
		}
	}
}
