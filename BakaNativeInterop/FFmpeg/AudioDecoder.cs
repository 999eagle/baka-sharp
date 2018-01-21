using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioDecoder : CodecContext
	{
		public InputContext Input { get; }
		int streamIndex;

		public AudioDecoder(InputContext inputContext, int streamIdx = -1)
		{
			try
			{
				Input = inputContext;
				streamIndex = streamIdx;
				if (streamIndex == -1)
				{
					for (int i = 0; i < Input.fmtContext->nb_streams; i++)
					{
						if (Input.fmtContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
						{
							streamIndex = i;
							break;
						}
					}
					if (streamIndex == -1)
					{
						throw new ArgumentException("No audio stream found in input.");
					}
				}
				else if (streamIndex >= Input.fmtContext->nb_streams)
				{
					throw new ArgumentOutOfRangeException("Specified stream index out of bounds.");
				}
				else if (Input.fmtContext->streams[streamIndex]->codecpar->codec_type != AVMediaType.AVMEDIA_TYPE_AUDIO)
				{
					throw new ArgumentException($"Specified stream at index {streamIndex} is not an audio stream.");
				}

				var stream = Input.fmtContext->streams[streamIndex];
				AVCodec* codec;
				if ((codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id)) == null)
				{
					throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to find decoder codec.");
				}
				if ((codecContext = ffmpeg.avcodec_alloc_context3(codec)) == null)
				{
					throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate decoder context.");
				}
				int ret;
				if ((ret = ffmpeg.avcodec_parameters_to_context(codecContext, stream->codecpar)) < 0)
				{
					throw new FFmpegException(ret, "Failed to copy stream parameters to decoder context.");
				}
				if ((ret = ffmpeg.avcodec_open2(codecContext, codec, null)) < 0)
				{
					throw new FFmpegException(ret, "Failed to open decoder context.");
				}
			}
			catch (Exception) when (this.DisposeOnException())
			{
			}
		}
	}
}
