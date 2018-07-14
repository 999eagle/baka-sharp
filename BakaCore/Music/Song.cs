using System;
using System.IO;
using System.Threading.Tasks;

using BakaCore.Data;

namespace BakaCore.Music
{
	public class Song
	{
		public SongMetadata Metadata { get; private set; }
		public string Id { get; private set; }
		public string FileId { get; private set; }

		private SongCollection collection;

		public Song(string id, SongMetadata metadata, string fileId, SongCollection collection)
		{
			Id = id;
			Metadata = metadata;
			FileId = fileId;
			this.collection = collection;
		}

		public async Task<Stream> GetOggStream()
		{
			return await collection.GetOggStream(this);
		}
	}

	public class SongMetadata
	{
		public string Title { get; private set; }
		public string Artist { get; private set; }
		public string Url { get; private set; }
		public TimeSpan Duration { get; private set; }

		public SongMetadata() { }

		public SongMetadata(string title, string artist, string url, TimeSpan duration)
		{
			Title = title;
			Artist = artist;
			Url = url;
			Duration = duration;
		}
	}
}
