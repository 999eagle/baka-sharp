using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace BakaCore.Music
{
	public class Playlist
	{
		public IList<string> songs = new List<string>();

		public string GetNextSong()
		{
			if (!songs.Any())
				return null;
			var song = songs[0];
			songs.RemoveAt(0);
			return song;
		}
	}
}
