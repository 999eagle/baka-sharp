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
		AudioDecoder decoder;
		AudioEncoder encoder;
		Resampler resampler;
		AudioFifoBuffer buffer;

		public AudioTranscoder(AudioDecoder audioDecoder, AudioEncoder audioEncoder)
		{
			decoder = audioDecoder;
			encoder = audioEncoder;

			resampler = new Resampler(audioDecoder, audioEncoder);
			buffer = new AudioFifoBuffer(audioEncoder.SampleFormat, audioEncoder.Channels);
		}

		void ReadDecodeConvertAndStore()
		{
			buffer.StoreFrameFromDecoder(decoder, resampler);
		}

		void LoadEncodeAndWrite()
		{
			buffer.WriteFrameToEncoder(encoder);
		}

		public void Transcode()
		{
			encoder.Output.WriteFileHeader();
			while (true)
			{
				int outputFrameSize = encoder.FrameSize;
				while (buffer.GetBufferSize() < outputFrameSize && !decoder.DecoderFlushed)
				{
					ReadDecodeConvertAndStore();
				}
				bool finished = decoder.DecoderFlushed;
				while (buffer.GetBufferSize() >= outputFrameSize || (finished && buffer.GetBufferSize() > 0))
				{
					LoadEncodeAndWrite();
				}
				if (finished)
				{
					encoder.Flush();
					break;
				}
			}
			encoder.Output.WriteFileTrailer();
		}

		#region Disposing
		private bool disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				// Dispose managed resources
				resampler.Dispose();
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
