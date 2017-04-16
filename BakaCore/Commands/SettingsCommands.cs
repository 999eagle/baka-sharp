using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;
using BakaCore.Data;

namespace BakaCore.Commands
{
	class SettingsCommands
	{
		private IDataStore dataStore;
		private ILogger logger;

		public SettingsCommands(IServiceProvider services)
		{
			dataStore = services.GetRequiredService<IDataStore>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SettingsCommands>();
		}

		[Command("settings", Subcommand = "set", Help = "Change a setting.")]
		public async Task SetSettingCommand(SocketMessage message, string setting, [CustomUsageText("<value>")][Optional]string[] args)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			switch (setting)
			{
				case "welcome":
				case "welcomechannel":
					await message.Channel.SendMessageAsync("Type \"yes\" within 5 seconds to announce newly joined users in this channel.");
					var answer = await channel.WaitForMessage(TimeSpan.FromSeconds(5), m => (m.Channel == message.Channel && m.Author == message.Author && m.Content == "yes"));
					if (answer == null)
					{
						await channel.SendMessageAsync("Time expired; nothing was changed.");
					}
					else
					{
						dataStore.GetGuildData(channel.Guild).SetWelcomeChannel(channel.Id);
						await channel.SendMessageAsync("New users will be announced in this channel from now on.");
					}
					break;
			}
		}
	}
}
