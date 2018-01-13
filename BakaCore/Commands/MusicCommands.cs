using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;


using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace BakaCore.Commands
{
    class MusicCommands
    {
		private DiscordSocketClient client;
		private IDictionary<ulong, (IAudioClient client, IAudioChannel channel)> audioClients = new Dictionary<ulong, (IAudioClient, IAudioChannel)>();

		public MusicCommands(IServiceProvider services)
		{
			client = services.GetRequiredService<DiscordSocketClient>();
		}

		private (IAudioClient client, IAudioChannel channel) GetAudioClient(SocketMessage senderMessage)
		{
			if (!(senderMessage.Channel is SocketGuildChannel channel))
			{
				return (null, null);
			}
			if (!audioClients.TryGetValue(channel.Guild.Id, out var client))
			{
				return (null, null);
			}
			else
			{
				return client;
			}
		}

		private async Task<(IAudioClient client, IAudioChannel channel)> StartOrGetAudioClient(SocketMessage senderMessage)
		{
			var (audioClient, channel) = GetAudioClient(senderMessage);
			if (audioClient != null && channel != null) return (audioClient, channel);
			if (!(senderMessage.Channel is SocketGuildChannel guildChannel))
			{
				await senderMessage.Channel.SendMessageAsync("Music can only be played on servers, sorry.");
				return (null, null);
			}
			var newAudioChannel = guildChannel.Guild.Channels.FirstOrDefault(c => c is IAudioChannel && c.Users.Any(u => u.Id == senderMessage.Author.Id)) as IAudioChannel;
			if (newAudioChannel == null)
			{
				await senderMessage.Channel.SendMessageAsync("Please join a voice channel first.");
				return (null, null);
			}
			audioClient = await newAudioChannel.ConnectAsync();
			audioClients[guildChannel.Guild.Id] = (audioClient, newAudioChannel);
			return (audioClient, newAudioChannel);
		}

		[Command("play", Scope = CommandScope.Guild)]
		public async Task PlayCommand(SocketMessage message, string text)
		{
			var audioClient = (await StartOrGetAudioClient(message)).client;
		}

		[Command("stop")]
		public async Task StopCommand(SocketMessage message)
		{
			var audioData = GetAudioClient(message);
			if (audioData.client == null) { return; }
			await audioData.client.StopAsync();
			if (audioData.channel is IGuildChannel guildChannel)
			{
				audioClients.Remove(guildChannel.GuildId);
			}
		}
	}
}
