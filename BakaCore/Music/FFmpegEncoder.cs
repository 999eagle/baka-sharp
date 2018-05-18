using System;
using System.IO;
using System.Threading.Tasks;

using BakaNativeInterop.FFmpeg;

namespace BakaCore.Music
{
	public class FFmpegEncoder
	{
		public Task<Stream> EncodeAsOggOpusAsync(Stream audioStream)
		{
			return Task.Run(() => EncodeAsOggOpus(audioStream));
		}

		public Stream EncodeAsOggOpus(Stream audioStream)
		{
			var oggStream = new MemoryStream();
			using (var inputStream = new AVIOStream(audioStream, FileAccess.Read))
			using (var inputCtx = new InputContext(inputStream))
			using (var decoder = new AudioDecoder(inputCtx))
			using (var outputStream = new AVIOStream(oggStream, FileAccess.Write))
			using (var outputCtx = new OutputContext(outputStream))
			{
				outputCtx.GuessOutputFormat(null, ".ogg", null);
				using (var encoder = new AudioEncoder(outputCtx, FFmpeg.AutoGen.AVCodecID.AV_CODEC_ID_OPUS, 48000, 2, 128000))
				using (var transcoder = new AudioTranscoder(encoder, decoder))
				{
					outputCtx.WriteFileHeader();
					transcoder.Transcode();
					outputCtx.WriteFileTrailer();
				}
			}
			oggStream.Position = 0;
			return oggStream;
		}
	}
}
