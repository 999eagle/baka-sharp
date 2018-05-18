using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BakaCore.Music
{
	public class Playlist
	{
		public IList<string> songs = new List<string>();

		public string GetNextSong()
		{
			var song = songs[0];
			songs.RemoveAt(0);
			return song;
		}
	}
}
