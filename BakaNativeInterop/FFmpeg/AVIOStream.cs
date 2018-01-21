using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AVIOStream : IDisposable
	{
		Stream stream;
		internal AVIOContext* ioContext;
		const uint DefaultBufferSize = 4096;
		GCHandle readPacketCallbackHandle;
		GCHandle writePacketCallbackHandle;

		public FileAccess Access { get; private set; }
		public bool CanRead { get { return Access.HasFlag(FileAccess.Read); } }
		public bool CanWrite { get { return Access.HasFlag(FileAccess.Write); } }
		
		public AVIOContext* GetInternalAVIOContext()
		{
			return ioContext;
		}

		public AVIOStream(Stream stream, FileAccess access)
		{
			this.stream = stream;
			Access = access;
			if (CanRead && !stream.CanRead) throw new ArgumentException("Can't read from stream");
			if (CanWrite && !stream.CanWrite) throw new ArgumentException("Can't write to stream");

			byte* ioBuffer = (byte*)ffmpeg.av_malloc(DefaultBufferSize);
			if (ioBuffer == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate I/O buffer");
			}
			int writeFlag = 0;
			avio_alloc_context_read_packet readPacket = null;
			avio_alloc_context_write_packet writePacket = null;
			if (CanWrite)
			{
				writeFlag = 1;
				writePacket = WritePacket;
				writePacketCallbackHandle = GCHandle.Alloc(writePacket);
			}
			if (CanRead)
			{
				readPacket = ReadPacket;
				readPacketCallbackHandle = GCHandle.Alloc(readPacket);
			}
			ioContext = ffmpeg.avio_alloc_context(ioBuffer, (int)DefaultBufferSize, writeFlag, null, readPacket, writePacket, null);
			if (ioContext == null)
			{
				ffmpeg.av_free(ioBuffer);
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate I/O context");
			}
		}

		private int ReadPacket(void* opaque, byte* buf, int bufSize)
		{
			byte[] buffer = new byte[bufSize];
			bufSize = stream.Read(buffer, 0, bufSize);
			Marshal.Copy(buffer, 0, (IntPtr)buf, bufSize);
			return bufSize;
		}

		private int WritePacket(void* opaque, byte* buf, int bufSize)
		{
			byte[] buffer = new byte[bufSize];
			Marshal.Copy((IntPtr)buf, buffer, 0, bufSize);
			stream.Write(buffer, 0, bufSize);
			return bufSize;
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
			if (readPacketCallbackHandle.IsAllocated)
			{
				readPacketCallbackHandle.Free();
			}
			if (writePacketCallbackHandle.IsAllocated)
			{
				writePacketCallbackHandle.Free();
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
		~AVIOStream()
		{
			Dispose(false);
		}
		#endregion
	}
}
