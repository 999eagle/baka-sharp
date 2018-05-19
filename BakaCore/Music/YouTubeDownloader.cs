using System;
using System.IO;
using System.Threading.Tasks;

using YoutubeExplode;
using YoutubeExplode.Models;
using YoutubeExplode.Models.MediaStreams;

namespace BakaCore.Music
{
	public class YouTubeDownloader
	{
		public class VideoInfo
		{
			private YoutubeClient client;
			private FFmpegEncoder encoder;
			private string videoId;

			public SongMetadata Metadata { get; private set; }

			private VideoInfo() {}

			internal static async Task<VideoInfo> CreateVideoInfo(YoutubeClient client, FFmpegEncoder encoder, string videoId)
			{
				var info = new VideoInfo()
				{
					client = client,
					encoder = encoder,
					videoId = videoId
				};
				await info.GetMetadata();
				return info;
			}

			private async Task GetMetadata()
			{
				var info = await client.GetVideoAsync(videoId);
				Metadata = new SongMetadata(info.Title, info.Author, info.GetUrl(), info.Duration);
			}

			public async Task<Stream> GetOggAudioStream()
			{
				var info = await client.GetVideoMediaStreamInfosAsync(videoId);
				var audio = info.Audio.WithHighestBitrate();
				var youtubeStream = await client.GetMediaStreamAsync(audio);
				return await encoder.EncodeAsOggOpusAsync(youtubeStream);
			}
		}

		private YoutubeClient client;
		private FFmpegEncoder encoder;

		public YouTubeDownloader()
		{
			client = new YoutubeClient();
			encoder = new FFmpegEncoder();
		}

		public async Task<VideoInfo> GetVideoInfo(string videoId)
		{
			return await VideoInfo.CreateVideoInfo(client, encoder, videoId);
		}
	}
}
