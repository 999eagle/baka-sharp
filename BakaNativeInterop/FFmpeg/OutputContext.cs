using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	unsafe class OutputContext : IDisposable
	{
		Stream outputStream;
		internal AVFormatContext* fmtContext;
		AVIOContext* ioContext;

		private OutputContext() { }

		private int WritePacket(void* opaque, byte* buf, int buf_size)
		{
			var unmanagedStream = new UnmanagedMemoryStream(buf, buf_size);
			var startPos = outputStream.Position;
			unmanagedStream.CopyTo(outputStream);
			return (int)(outputStream.Position - startPos);
		}

		public static OutputContext Create(Stream outputStream, string outputFormatExtension)
		{
			if (!outputStream.CanWrite) throw new ArgumentException("Can't write to stream");
			var inst = new OutputContext();
			inst.outputStream = outputStream;
			inst.fmtContext = ffmpeg.avformat_alloc_context();
			if (inst.fmtContext == null)
			{
				inst.Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM));
			}
			byte* ioContextBuffer = (byte*)ffmpeg.av_malloc(4096);
			if (ioContextBuffer == null)
			{
				inst.Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM));
			}
			inst.ioContext = ffmpeg.avio_alloc_context(ioContextBuffer, 4096, 1, null, null, (avio_alloc_context_write_packet)inst.WritePacket, null);
			if (inst.ioContext == null)
			{
				ffmpeg.av_free(ioContextBuffer);
				inst.Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM));
			}

			inst.fmtContext->pb = inst.ioContext;
			inst.fmtContext->oformat = ffmpeg.av_guess_format(null, outputFormatExtension, null);
			if (inst.fmtContext->oformat == null)
			{
				inst.Dispose();
				throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Couldn't find output format");
			}

			return inst;
		}

		#region Disposing
		private bool disposed = false;
		protected virtual void Dispose(bool disposing)
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
			if (ioContext != null)
			{
				ffmpeg.av_freep(&ioContext->buffer);
				fixed (AVIOContext** ioContextPtr = &ioContext)
				{
					ffmpeg.avio_context_free(ioContextPtr);
				}
			}
			disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~OutputContext()
		{
			Dispose(false);
		}
		#endregion
	}
}
