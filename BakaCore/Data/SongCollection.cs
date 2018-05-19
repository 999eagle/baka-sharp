using System;
using System.IO;
using System.Linq;
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

		internal SongCollection(LiteDatabase db, ILoggerFactory loggerFactory, IServiceProvider services)
		{
			logger = loggerFactory.CreateLogger<SongCollection>();
			collection = db.GetCollection<SongData>();
			fileStorage = db.FileStorage;
			downloader = new YouTubeDownloader(services.GetRequiredService<IMusicEncoderService>());
		}

		public async Task<SongData> GetSong(string songId)
		{
			return await Task.Run(() => collection.Find(d => d.Id == songId, 0, 1).FirstOrDefault());
		}

		public async Task<Stream> GetOggStream(Song song)
		{
			var data = await GetSong(song.Id);
			if (data == null) return null;
			return await Task.Run(() => fileStorage.OpenRead(data.FileId));
		}

		public async Task<string> AddSongFromYoutube(string videoId)
		{
			var songId = $"youtube/{videoId}";
			var songData = await GetSong(songId);
			if (songData != null) return songData.Id;

			logger.LogDebug($"Downloading new song from Youtube (Video ID: {videoId})");
			var videoInfo = await downloader.GetVideoInfo(videoId);
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
				Metadata = videoInfo.Metadata
			};
			await Task.Run(() => collection.Upsert(songData));
			return songId;
		}
	}
}
