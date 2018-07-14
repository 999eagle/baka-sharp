using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using BakaNativeInterop.FFmpeg;

namespace BakaCore.Services
{
	public class FFmpegEncoderService : IMusicEncoderService
	{
		private ILogger logger;
		private Configuration config;

		public FFmpegEncoderService(ILoggerFactory loggerFactory, Configuration config)
		{
			logger = loggerFactory.CreateLogger<FFmpegEncoderService>();
			this.config = config;
		}

		public Task<Stream> EncodeAsOggOpusAsync(Stream audioStream)
		{
			return Task.Run(() => EncodeAsOggOpus(audioStream));
		}

		public Stream EncodeAsOggOpus(Stream audioStream)
		{
			var oggStream = new MemoryStream();
			logger.LogInformation("Transcoding audio stream to ogg");
			logger.LogDebug($"Transcoding with {config.Music.EncodingSampleRate} samples and {config.Music.EncodingBitrate} bits per second");
			try
			{
				using (var inputStream = new AVIOStream(audioStream, FileAccess.Read))
				using (var inputCtx = new InputContext(inputStream))
				using (var decoder = new AudioDecoder(inputCtx))
				using (var outputStream = new AVIOStream(oggStream, FileAccess.Write))
				using (var outputCtx = new OutputContext(outputStream))
				{
					outputCtx.GuessOutputFormat(null, ".ogg", null);
					using (var encoder = new AudioEncoder(outputCtx, FFmpeg.AutoGen.AVCodecID.AV_CODEC_ID_OPUS, config.Music.EncodingSampleRate, 2, config.Music.EncodingBitrate))
					using (var transcoder = new AudioTranscoder(encoder, decoder))
					{
						outputCtx.WriteFileHeader();
						transcoder.Transcode(true);
						outputCtx.WriteFileTrailer();
						logger.LogDebug("Transcoding finished");
					}
				}
				oggStream.Position = 0;
				return oggStream;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Exception while transcoding audio stream");
				oggStream.Dispose();
				return null;
			}
		}
	}
}
