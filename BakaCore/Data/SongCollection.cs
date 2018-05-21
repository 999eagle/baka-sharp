using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LiteDB;

using BakaCore.Data.Models;
using BakaCore.Music;
using BakaCore.Services;

namespace BakaCore.Data
{
	public class SongCollection
	{
		private LiteCollection<SongData> collection;
		private LiteStorage fileStorage;
		private YouTubeDownloader downloader;
		private ILogger logger;
		private Configuration config;
		private IDictionary<string, ManualResetEvent> currentDownloads = new Dictionary<string, ManualResetEvent>();
		private object currentDownloadsLock = new object();

		internal SongCollection(LiteDatabase db, ILoggerFactory loggerFactory, Configuration config, IServiceProvider services)
		{
			logger = loggerFactory.CreateLogger<SongCollection>();
			collection = db.GetCollection<SongData>();
			collection.EnsureIndex(d => d.Id);
			fileStorage = db.FileStorage;
			downloader = new YouTubeDownloader(services.GetRequiredService<IMusicEncoderService>());
			this.config = config;
		}

		public async Task<SongData> GetSong(string songId)
		{
			return await Task.Run(() =>
			{
				var song = collection.Find(d => d.Id == songId, 0, 1).FirstOrDefault();
				if (song == null) return null;
				if ((DateTime.Now - song.LastAccess) > config.Music.MaximumSongAgeTimeSpan)
				{
					collection.Delete(song.Id);
					return null;
				}
				song.LastAccess = DateTime.Now;
				collection.Update(song);
				return song;
			});
		}

		public Task CleanOldSongs()
		{
			return Task.Run(() =>
			{
				collection.Delete(d => (DateTime.Now - d.LastAccess) > config.Music.MaximumSongAgeTimeSpan);
			});
		}

		public async Task<Stream> GetOggStream(Song song)
		{
			var data = await GetSong(song.Id);
			if (data == null) return null;
			return await Task.Run(() =>
			{
				var dbStream = fileStorage.OpenRead(data.FileId);
				var buffer = new MemoryStream();
				dbStream.CopyTo(buffer);
				dbStream.Dispose();
				buffer.Position = 0;
				return buffer;
			});
		}

		public async Task<string> AddSongFromYoutube(string videoId)
		{
			var songId = $"youtube/{videoId}";
			var songData = await GetSong(songId);
			if (songData != null) return songData.Id;

			ManualResetEvent waitEvent;
			bool shouldWait = false;
			lock(currentDownloadsLock)
			{
				// check whether another task is already downloading the same song
				if (currentDownloads.ContainsKey(songId))
				{
					waitEvent = currentDownloads[songId];
					shouldWait = true;
				}
				else
				{
					// we're the first task, create an event for other tasks to wait on
					waitEvent = new ManualResetEvent(false);
					currentDownloads.Add(songId, waitEvent);
				}
			}

			if (shouldWait)
			{
				// this task has to wait
				await Task.Run(() => waitEvent.WaitOne());
				// done waiting, just return whatever the first task downloaded (might be null)
				songData = await GetSong(songId);
				return songData?.Id;
			}

			try
			{
				logger.LogDebug($"Downloading new song from Youtube (Video ID: {videoId})");
				var videoInfo = await downloader.GetVideoInfo(videoId);
				if (videoInfo.Metadata.Duration > config.Music.MaximumSongLengthTimeSpan)
				{
					throw new VideoTooLongException(config.Music.MaximumSongLengthTimeSpan);
				}
				if (videoInfo.Metadata.Duration == TimeSpan.Zero)
				{
					throw new VideoIsLivestreamException();
				}
				var stream = await videoInfo.GetOggAudioStream();
				if (stream == null)
				{
					logger.LogError($"Downloading Youtube video {videoId} failed");
					return null;
				}
				var fileId = $"$/music/oggopus/youtube/{videoId}";
				var info = await Task.Run(() => fileStorage.Upload(fileId, videoId, stream));
				songData = new SongData()
				{
					Id = songId,
					FileId = fileId,
					Metadata = videoInfo.Metadata,
					LastAccess = DateTime.Now
				};
				await Task.Run(() => collection.Upsert(songData));
				return songId;
			}
			finally
			{
				// signal other waiting tasks and delete the wait entry
				lock (currentDownloadsLock)
				{
					waitEvent.Set();
					currentDownloads.Remove(songId);
				}
			}
		}
	}
}
