using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

	public enum SongState
	{
		Finished,
		Skipped,
		Playing,
		Error
	}

	public class Player
	{
		public struct PlayHistoryEntry
		{
			public Song song;
			public SongState state;
		}

		public IVoiceChannel VoiceChannel { get; private set; }
		public Playlist Playlist { get; private set; }
		public IGuild Guild { get; private set; }
		public Task PlayerTask { get; private set; }
		public ConnectionState ConnectionState { get; private set; }
		public PlayerState PlayerState { get; private set; }
		public IImmutableList<PlayHistoryEntry> PlayedSongs { get { return ImmutableList.ToImmutableList(playedSongs); } }

		private IList<PlayHistoryEntry> playedSongs;
		private ILogger logger;
		private CancellationTokenSource tokenSource;
		private MusicService musicService;

		public Player(IGuild guild, ILoggerFactory loggerFactory, MusicService musicService)
		{
			logger = loggerFactory.CreateLogger<Player>();
			ConnectionState = ConnectionState.Disconnected;
			Guild = guild;
			PlayerState = PlayerState.Disconnected;
			this.musicService = musicService;
			playedSongs = new List<PlayHistoryEntry>();
		}

		internal async Task Stop()
		{
			if (PlayerState == PlayerState.Disconnected) return;
			tokenSource.Cancel();
			await PlayerTask;
		}

		internal void Start(IVoiceChannel channel, Playlist playlist)
		{
			if (PlayerState != PlayerState.Disconnected) return;
			if (channel.Guild != Guild) throw new ArgumentException("Given voice channel is not in this player's guild", nameof(channel));
			tokenSource = new CancellationTokenSource();
			PlayerTask = PlayMusic(channel, playlist);
		}

		private async Task PlayMusic(IVoiceChannel channel, Playlist playlist)
		{
			var token = tokenSource.Token;
			PlayerState = PlayerState.Idle;
			VoiceChannel = channel;
			Playlist = playlist;
			playedSongs.Clear();
			var logTag = $"[Music {VoiceChannel.GuildId}] ";
			logger.LogInformation($"{logTag}Starting music player in guild {Guild.Name}, channel {VoiceChannel.Name}");
			ConnectionState = ConnectionState.Connecting;

			IAudioClient audioClient = null;
			AudioOutStream discordStream = null;
			try
			{
				audioClient = await VoiceChannel.ConnectAsync();
				discordStream = audioClient.CreateOpusStream();
				logger.LogDebug($"{logTag}Connected");
				ConnectionState = ConnectionState.Connected;

				while (!token.IsCancellationRequested)
				{
					var songId = playlist.GetNextSong();
					if (songId == null)
					{
						break;
					}
					logger.LogDebug($"{logTag}Playing next song (Id: {songId})");
					var song = await musicService.GetSong(songId);
					if (song == null)
					{
						logger.LogWarning($"{logTag}Failed to get data for song id {songId}");
						continue;
					}
					var playHistoryEntry = new PlayHistoryEntry { song = song, state = SongState.Playing };
					var oggStream = await song.GetOggStream();
					if (oggStream == null)
					{
						logger.LogWarning($"{logTag}Failed to get ogg stream for current song (Id: {songId})");
						playHistoryEntry.state = SongState.Error;
						playedSongs.Add(playHistoryEntry);
						continue;
					}
					try
					{
						var opusStream = new OpusOggReadStream(null, oggStream);
						PlayerState = PlayerState.Playing;
						playedSongs.Add(playHistoryEntry);
						while(opusStream.HasNextPacket && !token.IsCancellationRequested)
						{
							var packet = opusStream.RetrieveNextPacket();
							await discordStream.WriteAsync(packet, 0, packet.Length);
						}
						playHistoryEntry.state = SongState.Finished;
						playedSongs[playedSongs.Count] = playHistoryEntry;
					}
					catch (Exception ex)
					{
						logger.LogError(ex, $"{logTag}Exception while playing, skipping to next track");
						playHistoryEntry.state = SongState.Error;
						playedSongs[playedSongs.Count] = playHistoryEntry;
					}
					finally
					{
						oggStream.Dispose();
					}
					PlayerState = PlayerState.Idle;
				}
			}
			catch(Exception ex)
			{
				logger.LogError(ex, $"{logTag}Exception in music player");
			}
			finally
			{
				logger.LogInformation($"{logTag}Stopping music player");
				VoiceChannel = null;
				Playlist = null;
				ConnectionState = ConnectionState.Disconnecting;
				discordStream?.Dispose();
				if (audioClient != null)
				{
					audioClient.Disconnected -= ClientDisconnected;
					audioClient.Dispose();
				}
				ConnectionState = ConnectionState.Disconnected;
				PlayerState = PlayerState.Disconnected;
				logger.LogDebug($"{logTag}Stopped music player");
			}


			Task ClientDisconnected(Exception ex)
			{
				return Task.Run(() => {
					if (ex != null)
					{
						logger.LogError(ex, "Audio client disconnected with exception");
					}
					tokenSource.Cancel();
				});
			}
		}
	}
}
