using System;
using System.Collections.Generic;
using System.Text;
using FFmpeg.AutoGen;

namespace BakaNativeInterop.FFmpeg
{
	internal unsafe class Util
	{
		public static IEnumerable<int> GetSupportedAudioSampleRates(AVCodec* codec)
		{
			var rate = codec->supported_samplerates;
			if (rate == null) return null;
			var list = new List<int>();
			do
			{
				list.Add(*rate);
			} while (*(++rate) != 0);
			return list;
		}
	}
}
