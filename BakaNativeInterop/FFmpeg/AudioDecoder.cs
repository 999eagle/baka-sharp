using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioDecoder : CodecContext
	{
		public InputContext Input { get; }
		public bool DecoderFlushed { get; private set; }
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

		private bool ReceiveFrame(AVFrame* frame)
		{
			int ret = ffmpeg.avcodec_receive_frame(codecContext, frame);
			if (ret == 0)
				return true;
			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
				return false;
			if (ret == ffmpeg.AVERROR_EOF)
			{
				DecoderFlushed = true;
				return false;
			}
			throw new FFmpegException(ret, "Failed to receive frame from decoder.");
		}

		private bool SendPacket(AVPacket* packet)
		{
			int ret = ffmpeg.avcodec_send_packet(codecContext, packet);
			if (ret == 0)
				return true;
			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
				return false;
			throw new FFmpegException(ret, "Failed to send packet to decoder.");
		}

		public bool GetNextAudioFrame(AVFrame* frame)
		{
			if (ReceiveFrame(frame))
			{
				return true;
			}

			AVPacket packet;
			ffmpeg.av_init_packet(&packet);
			packet.data = null;
			packet.size = 0;
			int ret;
			if ((ret = ffmpeg.av_read_frame(Input.fmtContext, &packet)) < 0)
			{
				if (ret != ffmpeg.AVERROR_EOF)
				{
					throw new FFmpegException(ret, "Failed to read frame.");
				}
			}
			try
			{
				SendPacket(&packet);
			}
			finally
			{
				ffmpeg.av_packet_unref(&packet);
			}

			return ReceiveFrame(frame);
		}
	}
}
