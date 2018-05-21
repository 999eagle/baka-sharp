using System;
using System.Threading.Tasks;

using BakaCore.Data;
using BakaCore.Music;

namespace BakaCore.Services
{
	public class MusicService
	{
		private SongCollection songCollection;

		public MusicService(LiteDBStore dataStore)
		{
			songCollection = dataStore.SongCollection;
		}

		public async Task<Song> GetSong(string songId)
		{
			var data = await songCollection.GetSong(songId);
			if (data == null) return null;
			return new Song(data.Id, data.Metadata, data.FileId, songCollection);
		}

		public async Task<Song> DownloadFromYoutube(string videoId)
		{
			var songId = await songCollection.AddSongFromYoutube(videoId);
			if (songId == null) return null;
			return await GetSong(songId);
		}

		public Task CleanOldSongs()
		{
			return songCollection.CleanOldSongs();
		}
	}
}
