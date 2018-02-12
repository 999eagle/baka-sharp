using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioSampleBuffer : IDisposable
	{
		bool wasAllocated;
		internal byte** sampleBuffer;

		public AudioSampleBuffer(AVSampleFormat sampleFormat, int numChannels, int numSamples)
		{
			try
			{
				wasAllocated = true;
				Util.InitSampleBuffer(ref sampleBuffer, sampleFormat, numChannels, numSamples);
			}
			catch (Exception) when (this.DisposeOnException())
			{
			}
		}

		public AudioSampleBuffer(AVFrame* frame)
		{
			sampleBuffer = frame->extended_data;
		}

		public byte** GetInternalBufferPointer()
		{
			return sampleBuffer;
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
			if (wasAllocated)
			{
				Util.FreeSampleBuffer(ref sampleBuffer);
			}
			disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~AudioSampleBuffer()
		{
			Dispose(false);
		}
		#endregion
	}
}
