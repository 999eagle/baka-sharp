﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.WebSocket;

namespace BakaCore.Commands
{
	class GeneralCommands
	{
		private Random rand;

		public GeneralCommands(IServiceProvider services)
		{
			rand = services.GetRequiredService<Random>();
		}

		[Command("mods", Help = "Shows the mods on the server.")]
		public async Task ModsCommand(SocketMessage message)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				var guild = channel.Guild;
				var text = "";
				foreach (var user in guild.Users)
				{
					if (user.IsBot) continue;
					if (user.GuildPermissions.ManageMessages || user.GuildPermissions.ManageGuild || user.GuildPermissions.KickMembers || user.GuildPermissions.BanMembers)
					{
						switch (user.Status)
						{
							case UserStatus.Online:
								text += ":large_blue_circle:";
								break;
							case UserStatus.Idle:
								text += ":red_circle:";
								break;
							default:
								text += ":black_circle:";
								break;
						}
						string name = String.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname;
						text += $" **{name}**\n";
					}
				}
				if (text == "")
				{
					text = "No mods are on this server";
				}
				await channel.SendMessageAsync(text);
			}
		}

		[Command("info", Help = "Shows information about a user.")]
		public async Task InfoCommand(SocketMessage message, SocketUser user)
		{
			var text = $"Information about {user.Mention}\n**Username:** {user.Username}#{user.Discriminator}\n**Created:** {user.CreatedAt.ToString("yyyy-MM-dd HH:mm")}\n**Avatar URL:** {user.GetAvatarUrl()}";
			await message.Channel.SendMessageAsync(text);
		}

		[Command("ping", Help = "Get current ping time to the Discord servers.")]
		public async Task PingCommand(SocketMessage message)
		{
			await message.Channel.SendMessageAsync($"Pong\nRTT: {message.Discord.Latency}ms");
		}

		[Command("roll", Help = "Generate a random number between 1 and <number> (both inclusive).")]
		public async Task<bool> RollCommand(SocketMessage message, [CustomUsageText("D<number>")]string argument)
		{
			if (argument.StartsWith("D", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(argument.Substring(1), out int number))
			{
				if (number <= 1)
					await message.Channel.SendMessageAsync("Try a number greater than 1, baka!");
				else
					await message.Channel.SendMessageAsync($"Rolling a {number} sided :game_die:...\nRolled a {rand.Next(number) + 1}.");
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
