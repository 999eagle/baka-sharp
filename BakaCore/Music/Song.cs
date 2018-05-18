using System;
using System.IO;

namespace BakaCore.Music
{
	public class Song
	{
		public SongMetadata Metadata { get; private set; }
		public string Id { get; private set; }

		public Song(string id, SongMetadata metadata)
		{
			Id = id;
			Metadata = metadata;
		}
	}

	public class SongMetadata
	{
		public string Title { get; private set; }
		public string Artist { get; private set; }
		public string Url { get; private set; }
		public TimeSpan Duration { get; private set; }

		public SongMetadata(string title, string artist, string url, TimeSpan duration)
		{
			Title = title;
			Artist = artist;
			Url = url;
			Duration = duration;
		}
	}
}
