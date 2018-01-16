using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public sealed unsafe class OutputContext : FormatContext
	{
		public OutputContext(AVIOStream ioStream)
		{
			if (!ioStream.CanWrite) throw new ArgumentException("Can't write to stream");
			fmtContext = ffmpeg.avformat_alloc_context();
			if (fmtContext == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate format context");
			}
			fmtContext->pb = ioStream.ioContext;
		}

		public void SetOutputFormat(AVOutputFormat* format)
		{
			if (fmtContext->oformat != null) throw new InvalidOperationException("Output format already set");
			fmtContext->oformat = format;
		}
		public void GuessOutputFormat(string shortName, string filename, string mimeType)
		{
			if (fmtContext->oformat != null) throw new InvalidOperationException("Output format already set");
			fmtContext->oformat = ffmpeg.av_guess_format(shortName, filename, mimeType);
			if (fmtContext->oformat == null) throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to guess output format");
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
				ffmpeg.avformat_free_context(fmtContext);
			}
			disposed = true;
		}
		#endregion
	}
}
