using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public abstract unsafe class CodecContext : IDisposable
	{
		internal AVCodecContext* codecContext;
		private int[] supportedSampleRates;

		public int Channels { get { return codecContext->channels; } }
		public ulong ChannelLayout { get { return codecContext->channel_layout; } }
		public AVSampleFormat SampleFormat { get { return codecContext->sample_fmt; } }
		public int SampleRate { get { return codecContext->sample_rate; } }
		public long BitRate { get { return codecContext->bit_rate; } }
		public int FrameSize { get { return codecContext->frame_size; } }

		public AVCodecContext* GetInternalAVCodecContext()
		{
			return codecContext;
		}

		public int[] GetSupportedSampleRates()
		{
			if (supportedSampleRates != null) return supportedSampleRates;
			int* rate;
			if ((rate = codecContext->codec->supported_samplerates) == null) return null;
			var list = new List<int>();
			do
			{
				list.Add(*rate);
			} while (*(++rate) != 0);
			return (supportedSampleRates = list.ToArray());
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
			if (codecContext != null)
			{
				fixed (AVCodecContext** codecContextPtr = &codecContext)
				{
					ffmpeg.avcodec_free_context(codecContextPtr);
				}
			}
			disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~CodecContext()
		{
			Dispose(false);
		}
		#endregion
	}
}
