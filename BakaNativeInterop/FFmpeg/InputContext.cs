using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public sealed unsafe class InputContext : FormatContext
	{
		public InputContext(AVIOStream ioStream)
		{
			if (!ioStream.CanRead) throw new ArgumentException("Can't read from stream");
			fmtContext = ffmpeg.avformat_alloc_context();
			if (fmtContext == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate format context");
			}
			fmtContext->pb = ioStream.ioContext;

			int ret = 0;
			fixed (AVFormatContext** fmtContextPtr = &fmtContext)
			{
				ret = ffmpeg.avformat_open_input(fmtContextPtr, null, null, null);
			}
			if (ret < 0)
			{
				// format context is freed on failure in avformat_open_input, no need to clean up manually
				throw new FFmpegException(ret, "Failed to open input");
			}
			ret = ffmpeg.avformat_find_stream_info(fmtContext, null);
			if (ret < 0)
			{
				fixed (AVFormatContext** fmtContextPtr = &fmtContext)
				{
					ffmpeg.avformat_close_input(fmtContextPtr);
				}
				throw new FFmpegException(ret, "Failed to read stream information");
			}
		}

		#region Disposing
		protected override void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				// Dispose managed resources
			}
			// Dispose unmanaged resources
			if (fmtContext != null)
			{
				fixed (AVFormatContext** fmtContextPtr = &fmtContext)
				{
					ffmpeg.avformat_close_input(fmtContextPtr);
				}
			}
			disposed = true;
		}
		#endregion
	}
}
