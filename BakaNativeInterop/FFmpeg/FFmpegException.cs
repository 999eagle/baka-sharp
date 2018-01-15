using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public class FFmpegException : Exception
	{
		public int ReturnCode { get; }
		public FFmpegException(int returnCode) : base(GetFFmpegErrorString(returnCode))
		{
			ReturnCode = returnCode;
		}
		public FFmpegException(int returnCode, string message) : base(message + "\n" + GetFFmpegErrorString(returnCode))
		{
			ReturnCode = returnCode;
		}

		private static unsafe string GetFFmpegErrorString(int returnCode)
		{
			byte[] errString = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
			int ret;
			fixed (byte* errPtr = errString)
			{
				ret = ffmpeg.av_strerror(returnCode, errPtr, (ulong)errString.Length);
			}
			if (ret < 0)
			{
				return "";
			}
			return $"FFmpeg error 0x{returnCode :x8}:" + new string(Encoding.Default.GetChars(errString));
		}
	}
}
