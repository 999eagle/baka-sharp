using System;

namespace BakaCore
{
	public class VideoTooLongException : Exception
	{
		public TimeSpan MaxLength { get; private set; }
		public VideoTooLongException(TimeSpan maxLength)
		{
			MaxLength = maxLength;
		}
	}

	public class VideoIsLivestreamException : Exception {}
}
