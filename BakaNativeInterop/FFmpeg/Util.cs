using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	internal unsafe static class Util
	{
		public static void InitPacket(AVPacket* packet)
		{
			ffmpeg.av_init_packet(packet);
			packet->data = null;
			packet->size = 0;
		}

		public static void InitInputFrame(AVFrame** frame)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate input frame.");
			}
		}

		public static void InitOutputFrame(AVFrame** frame, AudioEncoder encoder, int maxFrameSize)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate output frame.");
			}
			(*frame)->nb_samples = Math.Min(maxFrameSize, encoder.FrameSize);
			(*frame)->channel_layout = encoder.ChannelLayout;
			(*frame)->format = (int)encoder.SampleFormat;
			(*frame)->sample_rate = encoder.SampleRate;

			int ret;
			if ((ret = ffmpeg.av_frame_get_buffer(*frame, 0)) < 0)
			{
				ffmpeg.av_frame_free(frame);
				throw new FFmpegException(ret, "Failed to allocate output frame samples.");
			}
		}

		public static void InitSampleBuffer(ref byte** buffer, AVSampleFormat sampleFormat, int numChannels, int numSamples)
		{
			buffer = (byte**)Marshal.AllocHGlobal(numChannels * sizeof(byte*));
			if (buffer == null)
			{
				throw new OutOfMemoryException("Failed to allocate buffer memory.");
			}
			int ret;
			if ((ret = ffmpeg.av_samples_alloc(buffer, null, numChannels, numSamples, sampleFormat, 0)) < 0)
			{
				FreeSampleBuffer(ref buffer);
				throw new FFmpegException(ret, "Failed to allocate buffer samples.");
			}
		}

		public static void FreeSampleBuffer(ref byte** buffer)
		{
			if (buffer != null)
			{
				ffmpeg.av_freep(&buffer[0]);
				Marshal.FreeHGlobal((IntPtr)buffer);
				buffer = null;
			}
		}
	}
}
