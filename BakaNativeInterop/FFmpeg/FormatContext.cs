using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe abstract class FormatContext : IDisposable
	{
		internal AVFormatContext* fmtContext;

		public AVFormatContext* GetInternalAVFormatContext()
		{
			return fmtContext;
		}

		public bool HasFlag(int flag)
		{
			return (fmtContext->flags & flag) != 0;
		}

		#region Disposing
		protected bool disposed = false;
		protected abstract void Dispose(bool disposing);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~FormatContext()
		{
			Dispose(false);
		}
		#endregion
	}
}
