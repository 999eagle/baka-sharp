using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;

namespace BakaCore
{
	class CommandHandler
	{
		private DiscordSocketClient client;
		private Config config;
		private ILogger logger;

		public CommandHandler(ILoggerFactory loggerFactory, DiscordSocketClient client, Config config)
		{
			logger = loggerFactory.CreateLogger<CommandHandler>();
			this.client = client;
			this.config = config;
			this.client.MessageReceived += MessageReceived;
		}

		private async Task MessageReceived(SocketMessage message)
		{
			if (message.Content.Length == 0) return;
			logger.LogTrace($"Message received: {message.Content}");
			if (config.CommandsDisabled) return;

			// split message and normalize array for the tag
			var command = message.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			if (!config.CommandTag.EndsWith(" ") && command[0].StartsWith(config.CommandTag))
			{
				if (command[0].Length == config.CommandTag.Length)
				{
					command.RemoveAt(0);
				}
				else
				{
					command[0] = command[0].Substring(config.CommandTag.Length);
				}
				command.Insert(0, config.CommandTag);
			}
		}
	}
}
