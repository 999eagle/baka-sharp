using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.Net;
using Discord.WebSocket;
using BakaCore.Data;

namespace BakaCore.Commands
{
	class SettingsCommands
	{
		private IDataStore dataStore;
		private ILogger logger;
		private DiscordSocketClient client;

		public SettingsCommands(IServiceProvider services)
		{
			dataStore = services.GetRequiredService<IDataStore>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SettingsCommands>();
			client = services.GetRequiredService<DiscordSocketClient>();
		}

		[Command("settings", Subcommand = "set", Help = "Change a setting.", RequiredPermissions = Permissions.Settings, Scope = CommandScope.Guild)]
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
					var answer = await channel.WaitForMessageAsync(client, TimeSpan.FromSeconds(5), m => (m.Author == message.Author && m.Content == "yes"));
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

		[Command("settings", Subcommand = "get", Help = "Get a setting.", RequiredPermissions = Permissions.Settings, Scope = CommandScope.Guild)]
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

		[Command("perms", Help = "Displays permissions.", RequiredPermissions = Permissions.DisplayPermissions, Scope = CommandScope.Guild)]
		public async Task DisplayPermissionsCommand(SocketMessage message, IMentionable mention)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			var guildData = dataStore.GetGuildData(channel.Guild);
			Func<Permissions, bool> checkPermission;
			if (mention is SocketRole role)
			{
				checkPermission = (p => guildData.RoleHasPermission(role, p));
			}
			else if (mention is SocketUser user)
			{
				var guildUser = channel.GetUser(user.Id);
				checkPermission = (p => guildData.UserHasPermission(guildUser, p));
			}
			else
				return;
			var text = "";
			foreach (Permissions perm in Enum.GetValues(typeof(Permissions)))
			{
				if (perm != Permissions.None && checkPermission(perm))
				{
					if (text != "")
						text += ", ";
					text += perm.ToString();
				}
			}
			if (text == "")
				text = "no special permissions.";
			else
				text = "these permissions: " + text + ".";
			await channel.SendMessageAsync($"{mention.Mention} has " + text);
		}

		[Command("perms", Subcommand = "give", Help = "Gives permissions.", RequiredPermissions = Permissions.EditPermissions, Scope = CommandScope.Guild)]
		public async Task GivePermissionCommand(SocketMessage message, IMentionable mention, string permission)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			if (!(mention is SocketEntity<ulong> entity))
				return;
			if (!Enum.TryParse<Permissions>(permission, true, out var perm))
			{
				await channel.SendMessageAsync("Unknown permission.");
				return;
			}
			var guildData = dataStore.GetGuildData(channel.Guild);
			guildData.AddPermission(entity, perm);
			await channel.SendMessageAsync($"Added permission {perm.ToString()} to {mention.Mention}");
		}

		[Command("perms", Subcommand = "remove", Help = "Removes permissions.", RequiredPermissions = Permissions.EditPermissions, Scope = CommandScope.Guild)]
		public async Task RemovePermissionCommand(SocketMessage message, IMentionable mention, string permission)
		{
			if (!(message.Channel is SocketTextChannel channel))
				return;
			if (!(mention is SocketEntity<ulong> entity))
				return;
			if (!Enum.TryParse<Permissions>(permission, true, out var perm))
			{
				await channel.SendMessageAsync("Unknown permission.");
				return;
			}
			var guildData = dataStore.GetGuildData(channel.Guild);
			guildData.RemovePermission(entity, perm);
			await channel.SendMessageAsync($"Removed permission {perm.ToString()} from {mention.Mention}");
		}

		[Command("purge", Help = "Purge the last messages from the current channel.", RequiredPermissions = Permissions.Purge)]
		public async Task PurgeMessageCommand(SocketMessage message, int amount)
		{
			if (amount <= 0)
			{
				await message.Channel.SendMessageAsync("Try more than 0 messages, baka!");
				return;
			}
			try
			{
				await message.Channel.GetMessagesAsync(amount).ForEachAsync(msg =>
				{
					Task.WaitAll(msg.Select(m => m.DeleteAsync()).ToArray());
				});
			}
			catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is HttpException h && h.HttpCode == HttpStatusCode.Forbidden))
			{
				await message.Channel.SendMessageAsync("I don't have the permission to delete messages here.");
			}
		}
	}
}
