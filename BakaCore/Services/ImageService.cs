using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BakaCore.Services
{
	class ImageService
	{
		private Configuration config;
		private Random rand;
		private ILogger logger;

		internal static ImageService instance;

		public ImageService(Configuration config, Random rand, ILoggerFactory loggerFactory)
		{
			this.config = config;
			this.rand = rand;
			this.logger = loggerFactory.CreateLogger<ImageService>();
			instance = this;
		}

		public EmbedBuilder EmbedImage(EmbedBuilder builder, string imageKey)
		{
			if (!config.Images.ImageData.ContainsKey(imageKey))
			{
				logger.LogWarning($"No image data for key \"{imageKey}\" found!");
				return builder;
			}
			var imageData = config.Images.ImageData[imageKey];
			var imagePath = imageData.FileName;
			if (imageData.Count > 1)
			{
				imagePath = String.Format(imagePath, rand.Next(imageData.Count) + 1);
			}
			return builder.WithImageUrl(Path.Combine(config.Images.BaseURL, imagePath));
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
