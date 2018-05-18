using System;
using System.IO;
using System.Threading.Tasks;

using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace BakaCore.Music
{
	public class YouTubeDownloader
	{
		private YoutubeClient client;
		private FFmpegEncoder encoder;

		public YouTubeDownloader()
		{
			client = new YoutubeClient();
			encoder = new FFmpegEncoder();
		}

		public async Task<Stream> GetOggAudioStream(string videoId)
		{
			var info = await client.GetVideoMediaStreamInfosAsync(videoId);
			var audio = info.Audio.WithHighestBitrate();
			var youtubeStream = await client.GetMediaStreamAsync(audio);
			return await encoder.EncodeAsOggOpusAsync(youtubeStream);
		}
	}
}
