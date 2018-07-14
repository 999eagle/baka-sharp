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

		public AVStream* CreateNewStream(AVCodec* codec = null)
		{
			var stream = ffmpeg.avformat_new_stream(fmtContext, codec);
			if (stream == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to create new stream.");
			}
			return stream;
		}

		public bool OutputFormatHasFlag(int formatFlag)
		{
			return (fmtContext->oformat->flags & formatFlag) != 0;
		}

		public void WriteFileHeader()
		{
			int ret;
			if ((ret = ffmpeg.avformat_write_header(fmtContext, null)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write file header.");
			}
		}

		public void WriteFileTrailer()
		{
			int ret;
			if ((ret = ffmpeg.av_write_trailer(fmtContext)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write file trailer.");
			}
		}

		public void WriteFramePacket(AVPacket* packet)
		{
			int ret;
			if ((ret = ffmpeg.av_write_frame(fmtContext, packet)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write encoded packet to output.");
			}
		}

		public void WriteInterleavedFramePacket(AVPacket* packet)
		{
			int ret;
			if ((ret = ffmpeg.av_interleaved_write_frame(fmtContext, packet)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write encoded packet to output.");
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
				ffmpeg.avformat_free_context(fmtContext);
				fmtContext = null;
			}
			disposed = true;
		}
		#endregion
	}
}
