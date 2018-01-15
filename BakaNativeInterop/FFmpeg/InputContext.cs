using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	unsafe class InputContext : IDisposable
	{
		Stream inputStream;
		internal AVFormatContext* fmtContext;
		AVIOContext* ioContext;

		private InputContext() { }

		private int ReadPacket(void* opaque, byte* buf, int buf_size)
		{
			byte[] buffer = new byte[buf_size];
			buf_size = inputStream.Read(buffer, 0, buf_size);
			Marshal.Copy(buffer, 0, (IntPtr)buf, buf_size);
			return buf_size;
		}

		public static InputContext Create(Stream inputStream)
		{
			if (!inputStream.CanRead) throw new ArgumentException("Can't read from stream");
			var inst = new InputContext();
			inst.inputStream = inputStream;
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
			inst.ioContext = ffmpeg.avio_alloc_context(ioContextBuffer, 4096, 0, null, (avio_alloc_context_read_packet)inst.ReadPacket, null, null);
			if (inst.ioContext == null)
			{
				ffmpeg.av_free(ioContextBuffer);
				inst.Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM));
			}

			inst.fmtContext->pb = inst.ioContext;
			int ret = 0;
			fixed (AVFormatContext** fmtContextPtr = &inst.fmtContext)
			{
				ret = ffmpeg.avformat_open_input(fmtContextPtr, null, null, null);
			}
			if (ret < 0)
			{
				inst.Dispose();
				throw new FFmpegException(ret, "Failed to open input");
			}
			ret = ffmpeg.avformat_find_stream_info(inst.fmtContext, null);
			if (ret < 0)
			{
				inst.Dispose();
				throw new FFmpegException(ret, "Failed to read stream information");
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
				fixed (AVFormatContext** fmtContextPtr = &fmtContext)
				{
					ffmpeg.avformat_close_input(fmtContextPtr);
				}
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
		~InputContext()
		{
			Dispose(false);
		}
		#endregion
	}
}
