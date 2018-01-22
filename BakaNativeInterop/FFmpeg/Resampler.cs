using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe sealed class Resampler : IDisposable
	{
		SwrContext* resamplerContext;

		public Resampler(CodecContext input, CodecContext output)
		{
			try
			{
				if ((resamplerContext = ffmpeg.swr_alloc_set_opts(null, (long)output.ChannelLayout, output.SampleFormat, output.SampleRate, (long)input.ChannelLayout, input.SampleFormat, input.SampleRate, 0, null)) == null)
				{
					throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate resampler context.");
				}
				int ret;
				if ((ret = ffmpeg.swr_init(resamplerContext)) < 0)
				{
					throw new FFmpegException(ret, "Failed to initialize resampler context.");
				}
			}
			catch (Exception) when (this.DisposeOnException())
			{
			}
		}

		public void Resample(AVFrame* inputFrame, AVFrame* outputFrame)
		{
			int ret;
			if ((ret = ffmpeg.swr_convert_frame(resamplerContext, outputFrame, inputFrame)) < 0)
			{
				throw new FFmpegException(ret, "Failed to resample input.");
			}
		}

		public void Resample(byte** inputData, byte** outputData, int numSamples)
		{
			int ret;
			if ((ret = ffmpeg.swr_convert(resamplerContext, outputData, numSamples, inputData, numSamples)) < 0)
			{
				throw new FFmpegException(ret, "Failed to resample input.");
			}
		}

		#region Disposing
		private bool disposed = false;
		private void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				// Dispose managed resources
			}
			// Dispose unmanaged resources
			if (resamplerContext != null)
			{
				fixed (SwrContext** resamplerContextPtr = &resamplerContext)
				{
					ffmpeg.swr_free(resamplerContextPtr);
				}
			}
			disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~Resampler()
		{
			Dispose(false);
		}
		#endregion
	}
}
