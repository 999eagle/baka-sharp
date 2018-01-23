using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioEncoder : CodecContext
	{
		public OutputContext Output { get; }
		public bool EncoderFlushed { get; private set; }
		public long PresentationTimestamp { get; private set; }

		public AudioEncoder(OutputContext outputContext, AVCodecID codecID, int outputSampleRate = -1, int outputChannels = 2, long outputBitRate = 160000)
		{
			try
			{
				Output = outputContext;

				AVCodec* codec;
				if ((codec = ffmpeg.avcodec_find_encoder(codecID)) == null)
				{
					throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to find encoder codec.");
				}
				var stream = Output.CreateNewStream(codec);
				if ((codecContext = ffmpeg.avcodec_alloc_context3(codec)) == null)
				{
					throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate encoder context.");
				}
				codecContext->channels = outputChannels;
				codecContext->channel_layout = (ulong)ffmpeg.av_get_default_channel_layout(codecContext->channels);
				var supportedSampleRates = GetSupportedSampleRates();
				if (supportedSampleRates == null || supportedSampleRates.Contains(outputSampleRate))
				{
					if (outputSampleRate == -1)
					{
						throw new ArgumentException("Failed to determine sample rate.");
					}
					codecContext->sample_rate = outputSampleRate;
				}
				else
				{
					// Use closest available sample rate
					codecContext->sample_rate = supportedSampleRates
						.OrderBy(rate => Math.Abs(rate - outputSampleRate))
						.First();
				}
				codecContext->sample_fmt = codec->sample_fmts[0];
				codecContext->bit_rate = outputBitRate;
				stream->time_base.num = 1;
				stream->time_base.den = codecContext->sample_rate;
				if (Output.OutputFormatHasFlag(ffmpeg.AVFMT_GLOBALHEADER))
				{
					codecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
				}

				int ret;
				if ((ret = ffmpeg.avcodec_open2(codecContext, codec, null)) < 0)
				{
					throw new FFmpegException(ret, "Failed to open encoder context.");
				}
				if ((ret = ffmpeg.avcodec_parameters_from_context(stream->codecpar, codecContext)) < 0)
				{
					throw new FFmpegException(ret, "Failed to copy encoder context parameters to stream.");
				}
			}
			catch (Exception) when (this.DisposeOnException())
			{
			}
		}

		private bool SendFrame(AVFrame* frame)
		{
			int ret = ffmpeg.avcodec_send_frame(codecContext, frame);
			if (ret == 0)
				return true;
			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
				return false;
			throw new FFmpegException(ret, "Failed to send frame to encoder.");
		}

		private bool ReceivePacket(AVPacket* packet)
		{
			int ret = ffmpeg.avcodec_receive_packet(codecContext, packet);
			if (ret == 0)
				return true;
			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
				return false;
			if (ret == ffmpeg.AVERROR_EOF)
			{
				EncoderFlushed = true;
				return false;
			}
			throw new FFmpegException(ret, "Failed to receive packet from encoder.");
		}

		public bool WriteNextAudioFrame(AVFrame* frame)
		{
			if (frame != null)
			{
				frame->pts = PresentationTimestamp;
				PresentationTimestamp += frame->nb_samples;
			}
			if (!SendFrame(frame))
				return false;

			AVPacket packet;
			Util.InitPacket(&packet);
			try
			{
				if (!ReceivePacket(&packet))
					return false;
				Output.WriteFramePacket(&packet);
			}
			finally
			{
				ffmpeg.av_packet_unref(&packet);
			}
			return true;
		}
	}
}
