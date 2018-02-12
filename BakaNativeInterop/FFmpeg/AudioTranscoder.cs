using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public class AudioTranscoder : IDisposable
	{
		AudioDecoder decoder = null;
		AudioEncoder encoder;
		Resampler resampler = null;
		AudioFifoBuffer buffer;

		public AudioTranscoder(AudioEncoder audioEncoder, AudioDecoder audioDecoder = null)
		{
			decoder = audioDecoder;
			encoder = audioEncoder;

			if (audioDecoder != null)
			{
				resampler = new Resampler(audioDecoder, audioEncoder);
			}
			buffer = new AudioFifoBuffer(audioEncoder.SampleFormat, audioEncoder.Channels);
		}

		public void ChangeDecoder(AudioDecoder newDecoder)
		{
			if (resampler != null)
			{
				resampler.Dispose();
				resampler = null;
			}
			decoder = newDecoder;

			if (decoder != null)
			{
				resampler = new Resampler(decoder, encoder);
			}
		}

		public int GetStoredBufferSize() => buffer.GetBufferSize();
		public bool CanEncodeFrame() => GetStoredBufferSize() >= encoder.FrameSize;
		public bool HasDataStored() => GetStoredBufferSize() > 0;

		public void DecodeFrame()
		{
			buffer.StoreFrameFromDecoder(decoder, resampler);
		}

		public void EncodeFrame()
		{
			buffer.WriteFrameToEncoder(encoder);
		}

		public void Transcode(bool flushEncoder = false)
		{
			while (true)
			{
				while (!CanEncodeFrame() && !decoder.DecoderFlushed)
				{
					DecodeFrame();
				}
				while (CanEncodeFrame() || (flushEncoder && decoder.DecoderFlushed && HasDataStored()))
				{
					EncodeFrame();
				}
				if (decoder.DecoderFlushed)
				{
					if (flushEncoder)
					{
						encoder.Flush();
					}
					break;
				}
			}
		}

		#region Disposing
		private bool disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				// Dispose managed resources
				if (resampler != null)
				{
					resampler.Dispose();
				}
				buffer.Dispose();
			}
			// Dispose unmanaged resources
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~AudioTranscoder()
		{
			Dispose(false);
		}
		#endregion
	}
}
