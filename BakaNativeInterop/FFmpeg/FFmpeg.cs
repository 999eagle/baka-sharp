using System;
using System.Collections.Generic;
using System.Text;

using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	public static class FFmpeg
	{
		public static void SetLibraryPath(string libraryRootPath = null)
		{
			ffmpeg.RootPath = libraryRootPath ?? string.Empty;
		}

		public static string GetFFmpegVersionInfo() => ffmpeg.av_version_info();
	}
}
