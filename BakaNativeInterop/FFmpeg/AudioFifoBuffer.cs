using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioFifoBuffer : IDisposable
	{
		AVAudioFifo* audioFifo;

		public AudioFifoBuffer(AVSampleFormat sampleFormat, int numChannels, int initialSize = 1)
		{
			if ((audioFifo = ffmpeg.av_audio_fifo_alloc(sampleFormat, numChannels, initialSize)) == null)
			{
				Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate fifo buffer.");
			}
		}

		public int GetBufferSize()
		{
			return ffmpeg.av_audio_fifo_size(audioFifo);
		}

		public void AddSamples(AudioSampleBuffer buffer, int numSamples)
		{
			int ret;
			if ((ret = ffmpeg.av_audio_fifo_realloc(audioFifo, GetBufferSize() + numSamples)) < 0)
			{
				throw new FFmpegException(ret, "Failed to reallocate fifo buffer.");
			}
			if ((ret = ffmpeg.av_audio_fifo_write(audioFifo, (void**)buffer.sampleBuffer, numSamples)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write data to fifo buffer");
			}
		}

		public void StoreFrameFromDecoder(AudioDecoder decoder, Resampler resampler = null)
		{
			AVFrame* inputFrame = null;
			AudioSampleBuffer buffer = null;
			AudioSampleBuffer resampledBuffer = null;

			try
			{
				Util.InitInputFrame(&inputFrame);
				if (decoder.GetNextAudioFrame(inputFrame))
				{
					buffer = new AudioSampleBuffer(inputFrame);
					if (resampler != null)
					{
						resampler.Resample(buffer, out resampledBuffer, inputFrame->nb_samples);
						AddSamples(resampledBuffer, inputFrame->nb_samples);
					}
					else
					{
						AddSamples(buffer, inputFrame->nb_samples);
					}
				}
			}
			finally
			{
				if (buffer != null)
				{
					buffer.Dispose();
				}
				if (resampledBuffer != null)
				{
					resampledBuffer.Dispose();
				}
				ffmpeg.av_frame_free(&inputFrame);
			}
		}

		public void WriteFrameToEncoder(AudioEncoder encoder)
		{
			AVFrame* outputFrame = null;
			Util.InitOutputFrame(&outputFrame, encoder, GetBufferSize());
			try
			{
				byte*[] outputDataArray = outputFrame->data;
				fixed (byte** dataPtr = &outputDataArray[0])
				{
					if (ffmpeg.av_audio_fifo_read(audioFifo, (void**)dataPtr, outputFrame->nb_samples) < outputFrame->nb_samples)
					{
						throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to read data from fifo buffer.");
					}
				}
				encoder.WriteNextAudioFrame(outputFrame);
			}
			finally
			{
				ffmpeg.av_frame_free(&outputFrame);
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
			}
			// Dispose unmanaged resources
			if (audioFifo != null)
			{
				ffmpeg.av_audio_fifo_free(audioFifo);
				audioFifo = null;
			}
			disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		~AudioFifoBuffer()
		{
			Dispose(false);
		}
		#endregion
	}
}
