using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioTranscoder : IDisposable
	{
		AudioDecoder decoder;
		AudioEncoder encoder;
		Resampler resampler;
		AVAudioFifo* audioFifo;

		public AudioTranscoder(AudioDecoder audioDecoder, AudioEncoder audioEncoder)
		{
			decoder = audioDecoder;
			encoder = audioEncoder;

			resampler = new Resampler(audioDecoder, audioEncoder);

			// Create FIFO buffer
			if ((audioFifo = ffmpeg.av_audio_fifo_alloc(encoder.SampleFormat, encoder.Channels, 1)) == null)
			{
				Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate fifo buffer.");
			}
		}

		void InitInputFrame(AVFrame** frame)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate input frame.");
			}
		}

		void InitOutputFrame(AVFrame** frame, int frameSize)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate output frame.");
			}
			(*frame)->nb_samples = frameSize;
			(*frame)->channel_layout = encoder.ChannelLayout;
			(*frame)->format = (int)encoder.SampleFormat;
			(*frame)->sample_rate = encoder.SampleRate;

			int ret;
			if ((ret = ffmpeg.av_frame_get_buffer(*frame, 0)) < 0)
			{
				ffmpeg.av_frame_free(frame);
				throw new FFmpegException(ret, "Failed to allocate output frame samples.");
			}
		}

		void InitConvertedSamples(byte*** convertedInputSamples, int frameSize)
		{
			*convertedInputSamples = (byte**)Marshal.AllocHGlobal(encoder.Channels * sizeof(byte*));
			if (*convertedInputSamples == null)
			{
				throw new OutOfMemoryException("Failed to allocate converted input sample array.");
			}
			for (int i = 0; i < encoder.Channels; i++)
			{
				(*convertedInputSamples)[i] = null;
			}
			int ret;
			if ((ret = ffmpeg.av_samples_alloc(*convertedInputSamples, null, encoder.Channels, frameSize, encoder.SampleFormat, 0)) < 0)
			{
				ffmpeg.av_freep(&(*convertedInputSamples)[0]);
				Marshal.FreeHGlobal((IntPtr)(*convertedInputSamples));
				throw new FFmpegException(ret, "Failed to allocate converted input samples.");
			}
		}

		void AddSamplesToFifo(byte** convertedInputSamples, int frameSize)
		{
			int ret;
			if ((ret = ffmpeg.av_audio_fifo_realloc(audioFifo, ffmpeg.av_audio_fifo_size(audioFifo) + frameSize)) < 0)
			{
				throw new FFmpegException(ret, "Failed to reallocate fifo buffer.");
			}
			if (ffmpeg.av_audio_fifo_write(audioFifo, (void**)convertedInputSamples, frameSize) < frameSize)
			{
				throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to write data to fifo buffer.");
			}
		}

		void ReadDecodeConvertAndStore()
		{
			AVFrame* inputFrame = null;
			byte** convertedInputSamples = null;

			try
			{
				InitInputFrame(&inputFrame);
				bool dataPresent = decoder.GetNextAudioFrame(inputFrame);
				if (decoder.DecoderFlushed && !dataPresent)
					return;
				if (dataPresent)
				{
					InitConvertedSamples(&convertedInputSamples, inputFrame->nb_samples);
					resampler.Resample(inputFrame->extended_data, convertedInputSamples, inputFrame->nb_samples);
					AddSamplesToFifo(convertedInputSamples, inputFrame->nb_samples);
				}
			}
			finally
			{
				if (convertedInputSamples != null)
				{
					ffmpeg.av_freep(&convertedInputSamples[0]);
					Marshal.FreeHGlobal((IntPtr)convertedInputSamples);
				}
				ffmpeg.av_frame_free(&inputFrame);
			}
		}

		void LoadEncodeAndWrite()
		{
			AVFrame* outputFrame;
			int frameSize = Math.Min(ffmpeg.av_audio_fifo_size(audioFifo), encoder.FrameSize);
			InitOutputFrame(&outputFrame, frameSize);
			try
			{
				byte*[] dataArray = outputFrame->data;
				fixed (byte** dataPtr = &dataArray[0])
				{
					if (ffmpeg.av_audio_fifo_read(audioFifo, (void**)dataPtr, frameSize) < frameSize)
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

		public void Transcode()
		{
			encoder.Output.WriteFileHeader();
			while (true)
			{
				int outputFrameSize = encoder.FrameSize;
				while (ffmpeg.av_audio_fifo_size(audioFifo) < outputFrameSize && !decoder.DecoderFlushed)
				{
					ReadDecodeConvertAndStore();
				}
				bool finished = decoder.DecoderFlushed;
				while (ffmpeg.av_audio_fifo_size(audioFifo) >= outputFrameSize || (finished && ffmpeg.av_audio_fifo_size(audioFifo) > 0))
				{
					LoadEncodeAndWrite();
				}
				if (finished)
				{
					do
					{
						encoder.WriteNextAudioFrame(null);
					} while (!encoder.EncoderFlushed);
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
		~AudioTranscoder()
		{
			Dispose(false);
		}
		#endregion
	}
}
