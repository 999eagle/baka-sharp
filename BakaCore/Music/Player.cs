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
using Discord.Audio;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using Concentus.Structs;
using Concentus.Oggfile;

using BakaNativeInterop.FFmpeg;
using BakaCore.Services;

namespace BakaCore.Music
{
	public enum PlayerState
	{
		Disconnected,
		Idle,
		Playing
	}

	public class Player
	{
		public IVoiceChannel VoiceChannel { get; private set; }
		public IGuild Guild { get { return VoiceChannel.Guild; } }
		public Task PlayerTask { get; private set; }
		public ConnectionState ConnectionState { get; private set; }
		public PlayerState PlayerState { get; private set; }

		private ILogger logger;
		private CancellationTokenSource tokenSource;
		private MusicService musicService;

		public Player(IVoiceChannel channel, ILoggerFactory loggerFactory, MusicService musicService)
		{
			logger = loggerFactory.CreateLogger<Player>();
			ConnectionState = ConnectionState.Disconnected;
			VoiceChannel = channel;
			PlayerState = PlayerState.Disconnected;
			this.musicService = musicService;
		}

		public async Task Stop()
		{
			if (PlayerState == PlayerState.Disconnected) return;
			tokenSource.Cancel();
			await PlayerTask;
		}

		public void Start(Playlist playlist)
		{
			if (PlayerState != PlayerState.Disconnected) return;
			tokenSource = new CancellationTokenSource();
			PlayerTask = PlayMusic(playlist);
		}

		private async Task PlayMusic(Playlist playlist)
		{
			var token = tokenSource.Token;
			PlayerState = PlayerState.Idle;
			var logTag = $"[Music {VoiceChannel.GuildId}] ";
			logger.LogInformation($"{logTag}Starting music player in guild {Guild.Name}, channel {VoiceChannel.Name}");
			ConnectionState = ConnectionState.Connecting;
			var audioClient = await VoiceChannel.ConnectAsync();
			var discordStream = audioClient.CreateOpusStream();
			logger.LogDebug($"{logTag}Connected");
			ConnectionState = ConnectionState.Connected;

			while (!token.IsCancellationRequested)
			{
				var song = playlist.GetNextSong();
				if (song == null)
				{
					break;
				}
				var oggStream = await (await musicService.GetSong(song)).GetOggStream();
				var opusStream = new OpusOggReadStream(null, oggStream);
				PlayerState = PlayerState.Playing;
				while(opusStream.HasNextPacket && !token.IsCancellationRequested)
				{
					var packet = opusStream.RetrieveNextPacket();
					await discordStream.WriteAsync(packet, 0, packet.Length);
				}
				oggStream.Dispose();
				PlayerState = PlayerState.Idle;
			}

			ConnectionState = ConnectionState.Disconnecting;
			discordStream.Dispose();
			audioClient.Disconnected -= ClientDisconnected;
			audioClient.Dispose();
			ConnectionState = ConnectionState.Disconnected;
			PlayerState = PlayerState.Disconnected;


			async Task ClientDisconnected(Exception ex)
			{
				tokenSource.Cancel();
			}
		}
	}
}
