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

		[Command("settings", Subcommand = "set", Help = "Change a setting.", RequiredPermissions = Permissions.Settings)]
		public async Task SetSettingCommand(SocketMessage message, string setting, [CustomUsageText("<value>")][Optional]string[] args)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			var guildData = dataStore.GetGuildData(channel.Guild);
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
						guildData.SetWelcomeChannel(channel.Id);
						await channel.SendMessageAsync("New users will be announced in this channel from now on.");
					}
					break;
				default:
					await channel.SendMessageAsync("Unknown setting.");
					break;
			}
		}

		[Command("settings", Subcommand = "get", Help = "Get a setting.", RequiredPermissions = Permissions.Settings)]
		public async Task GetSettingCommand(SocketMessage message, string setting)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			var guildData = dataStore.GetGuildData(channel.Guild);
			switch (setting)
			{
				case "welcome":
				case "welcomechannel":
					var value = guildData.GetWelcomeChannel();
					if (value == 0)
					{
						await channel.SendMessageAsync("No welcome channel set.");
					}
					else
					{
						await channel.SendMessageAsync($"Current welcome channel: {(channel.Guild.GetChannel(value)?.Name)??"deleted channel"}");
					}
					break;
				default:
					await channel.SendMessageAsync("Unknown setting.");
					break;
			}
		}
	}
}
