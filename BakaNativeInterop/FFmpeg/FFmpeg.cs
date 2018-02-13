using System;
using System.Collections.Generic;
using System.Text;

using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public static class FFmpeg
	{
		public static void InitializeFFmpeg()
		{
			ffmpeg.av_register_all();
		}
	}
}
