using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord;
using BakaCore.Music;

namespace BakaCore.Services
{
	public class PlayerService : IDisposable
	{
		private ILoggerFactory loggerFactory;
		private MusicService musicService;
		private ILogger logger;
		private IDictionary<ulong, Player> currentPlayers = new Dictionary<ulong, Player>();

		public PlayerService(ILoggerFactory loggerFactory, MusicService musicService)
		{
			this.loggerFactory = loggerFactory;
			this.musicService = musicService;
			logger = loggerFactory.CreateLogger<PlayerService>();
		}

		public Player GetPlayerForGuild(IGuild guild)
		{
			if(currentPlayers.TryGetValue(guild.Id, out var player))
			{
				return player;
			}
			return null;
		}

		private Player GetOrCreatePlayerForGuild(IGuild guild)
		{
			if (guild == null) throw new ArgumentNullException(nameof(guild));
			var player = GetPlayerForGuild(guild);
			if (player != null) return player;

			player = new Player(guild, loggerFactory, musicService);
			currentPlayers.Add(guild.Id, player);
			return player;
		}

		public void EnqueueAndPlay(IVoiceChannel channel, string songId)
		{
			var player = GetOrCreatePlayerForGuild(channel.Guild);
			if (player.PlayerState == PlayerState.Disconnected)
			{
				var playlist = new Playlist();
				playlist.songs.Add(songId);
				player.Start(channel, playlist);
			}
			else
			{
				player.Playlist.songs.Add(songId);
			}
		}

		public async Task StopPlayerInGuild(IGuild guild)
		{
			var player = GetPlayerForGuild(guild);
			if (player == null) return;
			await player.Stop();
		}

		protected virtual void Dispose(bool disposing)
		{
			var tasks = new List<Task>();
			foreach(var kv in currentPlayers)
			{
				tasks.Add(kv.Value.Stop());
			}
			Task.WhenAll(tasks).Wait();
			currentPlayers.Clear();
		}

		public void Dispose()
		{
			Dispose(true);
		}
	}
}
