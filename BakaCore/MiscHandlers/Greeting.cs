using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;
using BakaCore.Data;

namespace BakaCore.MiscHandlers
{
	class Greeting
	{
		private ILogger logger;
		private DiscordSocketClient client;
		private IDataStore dataStore;

		public Greeting(ILoggerFactory loggerFactory, DiscordSocketClient client, IDataStore dataStore)
		{
			this.logger = loggerFactory.CreateLogger<Greeting>();
			this.client = client;
			this.dataStore = dataStore;
			this.client.UserJoined += UserJoined;
			logger.LogInformation("Initialized");
		}

		private async Task UserJoined(SocketGuildUser user)
		{
			var guildData = dataStore.GetGuildData(user.Guild);
			var channel = guildData.GetWelcomeChannel();
			if (channel != 0)
			{
				await user.Guild.GetTextChannel(channel)?.SendMessageAsync($"@here, user {user.Mention} has joined the guild for the first time.");
			}
		}
	}
}
