using System;
using System.Collections.Generic;
using System.Text;

using Discord;

namespace BakaCore
{
	class ImageService
	{
		private Configuration config;
		internal static ImageService instance;

		public ImageService(Configuration config)
		{
			this.config = config;
			instance = this;
		}

		public EmbedBuilder EmbedImage(EmbedBuilder builder, string imageKey)
		{
			return builder.WithImageUrl($"https://baka-chan.999eagle.moe/img/{imageKey}");
		}

		public EmbedBuilder EmbedImage(string imageKey)
		{
			return EmbedImage(new EmbedBuilder(), imageKey);
		}

		public Embed GetImageEmbed(string imageKey)
		{
			return EmbedImage(imageKey).Build();
		}
	}

	static class ImageServiceExtensions
	{
		public static EmbedBuilder EmbedImage(this EmbedBuilder builder, string imageKey)
		{
			if (ImageService.instance == null)
			{
				return builder;
			}
			else
			{
				return ImageService.instance.EmbedImage(builder, imageKey);
			}
		}
	}
}
