using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public unsafe class AudioTranscoder : IDisposable
	{
		InputContext input;
		OutputContext output;
		AVCodecContext* decoderContext;
		AVCodecContext* encoderContext;

		public AudioTranscoder(InputContext inputContext, OutputContext outputContext, AVCodecID outputCodecId, int outputChannels = 2, long outputBitRate = 160000)
		{
			input = inputContext;
			output = outputContext;
			if (input.fmtContext->nb_streams != 1) throw new ArgumentException($"Expected one audio stream in input, but found {inputContext.fmtContext->nb_streams} streams.");
			var inputCodec = ffmpeg.avcodec_find_decoder(input.fmtContext->streams[0]->codecpar->codec_id);
			if (inputCodec == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Couldn't find input codec.");
			}
			var outputCodec = ffmpeg.avcodec_find_encoder(outputCodecId);
			if (outputCodec == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Couldn't find output codec.");
			}

			decoderContext = ffmpeg.avcodec_alloc_context3(inputCodec);
			if (decoderContext == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Couldn't allocate decoding context.");
			}
			int ret = ffmpeg.avcodec_parameters_to_context(decoderContext, input.fmtContext->streams[0]->codecpar);
			if (ret < 0)
			{
				Dispose();
				throw new FFmpegException(ret, "Failed to copy stream parameters to decoding context.");
			}
			ret = ffmpeg.avcodec_open2(decoderContext, inputCodec, null);
			if (ret < 0)
			{
				Dispose();
				throw new FFmpegException(ret, "Couldn't open input codec.");
			}

			var outputStream = ffmpeg.avformat_new_stream(output.fmtContext, null);
			if (outputStream == null)
			{
				Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to create output stream.");
			}
			encoderContext = ffmpeg.avcodec_alloc_context3(outputCodec);
			if (encoderContext == null)
			{
				Dispose();
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Couldn't allocate encoding context.");
			}
			encoderContext->channels = outputChannels;
			encoderContext->channel_layout = (ulong)ffmpeg.av_get_default_channel_layout(encoderContext->channels);
			encoderContext->sample_rate = decoderContext->sample_rate;
			encoderContext->sample_fmt = outputCodec->sample_fmts[0];
			encoderContext->bit_rate = outputBitRate;
			outputStream->time_base.num = 1;
			outputStream->time_base.den = encoderContext->sample_rate;
			if ((output.fmtContext->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				encoderContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
			}

			ret = ffmpeg.avcodec_open2(encoderContext, outputCodec, null);
			if (ret < 0)
			{
				Dispose();
				throw new FFmpegException(ret, "Couldn't open output codec.");
			}
			ret = ffmpeg.avcodec_parameters_from_context(outputStream->codecpar, encoderContext);
			if (ret < 0)
			{
				Dispose();
				throw new FFmpegException(ret, "Failed to copy stream parameters from decoding context.");
			}
		}

		void InitPacket(AVPacket* packet)
		{
			ffmpeg.av_init_packet(packet);
			packet->data = null;
			packet->size = 0;
		}

		void InitInputFrame(AVFrame** frame)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate input frame.");
			}
		}

		void InitResampler(SwrContext** resamplerContext)
		{
			int ret;
			*resamplerContext = ffmpeg.swr_alloc_set_opts(null, (long)encoderContext->channel_layout, encoderContext->sample_fmt, encoderContext->sample_rate, (long)decoderContext->channel_layout, decoderContext->sample_fmt, decoderContext->sample_rate, 0, null);
			if (*resamplerContext == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate resample context.");
			}
			if ((ret = ffmpeg.swr_init(*resamplerContext)) < 0)
			{
				ffmpeg.swr_free(resamplerContext);
				throw new FFmpegException(ret, "Failed to init resample context.");
			}
		}

		void InitFifo(AVAudioFifo** fifo)
		{
			if ((*fifo = ffmpeg.av_audio_fifo_alloc(encoderContext->sample_fmt, encoderContext->channels, 1)) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate fifo buffer.");
			}
		}

		void WriteOutputFileHeader()
		{
			int ret;
			if ((ret = ffmpeg.avformat_write_header(output.fmtContext, null)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write output file header.");
			}
		}

		void DecodeAudioFrame(AVFrame* frame, int* dataPresent, ref bool finished)
		{
			AVPacket packet;
			InitPacket(&packet);

			int ret;
			if ((ret = ffmpeg.av_read_frame(input.fmtContext, &packet)) < 0)
			{
				if (ret == ffmpeg.AVERROR_EOF)
				{
					finished = true;
				}
				else
				{
					throw new FFmpegException(ret, "Failed to read frame.");
				}
			}
			if ((ret = ffmpeg.avcodec_decode_audio4(decoderContext, frame, dataPresent, &packet)) < 0)
			{
				ffmpeg.av_packet_unref(&packet);
				throw new FFmpegException(ret, "Failed to decode frame.");
			}
			if (finished && *dataPresent != 0)
				finished = false;
			ffmpeg.av_packet_unref(&packet);
		}

		void InitConvertedSamples(byte*** convertedInputSamples, int frameSize)
		{
			*convertedInputSamples = (byte**)Marshal.AllocHGlobal(encoderContext->channels * sizeof(byte*));
			if (*convertedInputSamples == null)
			{
				throw new OutOfMemoryException("Failed to allocate converted input sample array.");
			}
			for (int i = 0; i < encoderContext->channels; i++)
			{
				(*convertedInputSamples)[i] = null;
			}
			int ret;
			if ((ret = ffmpeg.av_samples_alloc(*convertedInputSamples, null, encoderContext->channels, frameSize, encoderContext->sample_fmt, 0)) < 0)
			{
				ffmpeg.av_freep(&(*convertedInputSamples)[0]);
				Marshal.FreeHGlobal((IntPtr)(*convertedInputSamples));
				throw new FFmpegException(ret, "Failed to allocate converted input samples.");
			}
		}

		void ConvertSamples(byte** inputData, byte** convertedData, int frameSize, SwrContext* resamplerContext)
		{
			int ret;
			if ((ret = ffmpeg.swr_convert(resamplerContext, convertedData, frameSize, inputData, frameSize)) < 0)
			{
				throw new FFmpegException(ret, "Failed to convert input samples.");
			}
		}

		void AddSamplesToFifo(AVAudioFifo* fifo, byte** convertedInputSamples, int frameSize)
		{
			int ret;
			if ((ret = ffmpeg.av_audio_fifo_realloc(fifo, ffmpeg.av_audio_fifo_size(fifo) + frameSize)) < 0)
			{
				throw new FFmpegException(ret, "Failed to reallocate fifo buffer.");
			}
			if (ffmpeg.av_audio_fifo_write(fifo, (void**)convertedInputSamples, frameSize) < frameSize)
			{
				throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to write data to fifo buffer.");
			}
		}

		void ReadDecodeConvertAndStore(AVAudioFifo* fifo, SwrContext* resamplerContext, ref bool finished)
		{
			AVFrame* inputFrame = null;
			byte** convertedInputSamples = null;
			int dataPresent;

			try
			{
				InitInputFrame(&inputFrame);
				DecodeAudioFrame(inputFrame, &dataPresent, ref finished);
				if (finished && dataPresent == 0)
					return;
				if (dataPresent != 0)
				{
					InitConvertedSamples(&convertedInputSamples, inputFrame->nb_samples);
					ConvertSamples(inputFrame->extended_data, convertedInputSamples, inputFrame->nb_samples, resamplerContext);
					AddSamplesToFifo(fifo, convertedInputSamples, inputFrame->nb_samples);
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

		void InitOutputFrame(AVFrame** frame, int frameSize)
		{
			if ((*frame = ffmpeg.av_frame_alloc()) == null)
			{
				throw new FFmpegException(ffmpeg.AVERROR(ffmpeg.ENOMEM), "Failed to allocate output frame.");
			}
			(*frame)->nb_samples = frameSize;
			(*frame)->channel_layout = encoderContext->channel_layout;
			(*frame)->format = (int)encoderContext->sample_fmt;
			(*frame)->sample_rate = encoderContext->sample_rate;

			int ret;
			if ((ret = ffmpeg.av_frame_get_buffer(*frame, 0)) < 0)
			{
				ffmpeg.av_frame_free(frame);
				throw new FFmpegException(ret, "Failed to allocate output frame samples.");
			}
		}

		long pts = 0;

		void EncodeAudioFrame(AVFrame* frame, int* dataPresent)
		{
			AVPacket outputPacket;
			InitPacket(&outputPacket);
			if (frame != null)
			{
				frame->pts = pts;
				pts += frame->nb_samples;
			}
			int ret;
			if ((ret = ffmpeg.avcodec_encode_audio2(encoderContext, &outputPacket, frame, dataPresent)) < 0)
			{
				ffmpeg.av_packet_unref(&outputPacket);
				throw new FFmpegException(ret, "Failed to encode frame.");
			}
			if (*dataPresent != 0)
			{
				if ((ret = ffmpeg.av_write_frame(output.fmtContext, &outputPacket)) < 0)
				{
					ffmpeg.av_packet_unref(&outputPacket);
					throw new FFmpegException(ret, "Failed to write frame.");
				}
				ffmpeg.av_packet_unref(&outputPacket);
			}
		}

		void LoadEncodeAndWrite(AVAudioFifo* fifo)
		{
			AVFrame* outputFrame;
			int frameSize = Math.Min(ffmpeg.av_audio_fifo_size(fifo), encoderContext->frame_size);
			int dataWritten;
			InitOutputFrame(&outputFrame, frameSize);
			try
			{
				byte*[] dataArray = outputFrame->data;
				fixed (byte** dataPtr = &dataArray[0])
				{
					if (ffmpeg.av_audio_fifo_read(fifo, (void**)dataPtr, frameSize) < frameSize)
					{
						throw new FFmpegException(ffmpeg.AVERROR_UNKNOWN, "Failed to read data from fifo buffer.");
					}
				}
				EncodeAudioFrame(outputFrame, &dataWritten);
			}
			finally
			{
				ffmpeg.av_frame_free(&outputFrame);
			}
		}

		void WriteOutputFileTrailer()
		{
			int ret;
			if ((ret = ffmpeg.av_write_trailer(output.fmtContext)) < 0)
			{
				throw new FFmpegException(ret, "Failed to write output file trailer.");
			}
		}

		public void Transcode()
		{
			SwrContext* resamplerContext = null;
			AVAudioFifo* fifo = null;
			try
			{
				InitResampler(&resamplerContext);
				InitFifo(&fifo);
				WriteOutputFileHeader();
				while (true)
				{
					int outputFrameSize = encoderContext->frame_size;
					bool finished = false;
					while (ffmpeg.av_audio_fifo_size(fifo) < outputFrameSize)
					{
						ReadDecodeConvertAndStore(fifo, resamplerContext, ref finished);
						if (finished)
							break;
					}
					while (ffmpeg.av_audio_fifo_size(fifo) >= outputFrameSize || (finished && ffmpeg.av_audio_fifo_size(fifo) > 0))
					{
						LoadEncodeAndWrite(fifo);
					}
					if (finished)
					{
						int dataWritten;
						do
						{
							EncodeAudioFrame(null, &dataWritten);
						} while (dataWritten != 0);
						break;
					}
				}
				WriteOutputFileTrailer();
			}
			finally
			{
				if (resamplerContext != null)
				{
					ffmpeg.swr_free(&resamplerContext);
				}
				if (fifo != null)
				{
					ffmpeg.av_audio_fifo_free(fifo);
					fifo = null;
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
			}
			// Dispose unmanaged resources
			if (decoderContext != null)
			{
				fixed (AVCodecContext** decoderContextPtr = &decoderContext)
				{
					ffmpeg.avcodec_free_context(decoderContextPtr);
				}
			}
			if (encoderContext != null)
			{
				fixed (AVCodecContext** encoderContextPtr = &encoderContext)
				{
					ffmpeg.avcodec_free_context(encoderContextPtr);
				}
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
