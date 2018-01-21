using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	internal unsafe static class Util
	{
		public static void InitPacket(AVPacket* packet)
		{
			ffmpeg.av_init_packet(packet);
			packet->data = null;
			packet->size = 0;
		}
	}
}
