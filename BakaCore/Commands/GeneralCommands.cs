using System;
using System.Collections.Generic;
using System.Numerics;
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
		private ImageService imageService;
		private DiscordSocketClient client;

		public GeneralCommands(IServiceProvider services)
		{
			rand = services.GetRequiredService<Random>();
			imageService = services.GetRequiredService<ImageService>();
			client = services.GetRequiredService<DiscordSocketClient>();
		}

		[Command("mods", Help = "Shows the mods on the server.", Scope = CommandScope.Guild)]
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
			var text = $"Information about {user.Mention}\n**Username:** {user.Username}#{user.Discriminator}\n**Created:** {user.CreatedAt.ToString("yyyy-MM-dd HH:mm")}";
			string avatarUrl = user.GetAvatarUrl();
			if (!String.IsNullOrEmpty(avatarUrl))
				text += $"\n**Avatar URL:** {avatarUrl}";
			await message.Channel.SendMessageAsync(text);
		}

		[Command("ping", Help = "Get current ping time to the Discord servers.")]
		public async Task PingCommand(SocketMessage message)
		{
			await message.Channel.SendMessageAsync($"Pong\nRTT: {client.Latency}ms");
		}

		[Command("roll", Help = "Generate a random number between 1 and <number> (both inclusive).")]
		public async Task<bool> RollCommand(SocketMessage message, [CustomUsageText("D<number>")]string argument)
		{
			if (argument.StartsWith("D", StringComparison.OrdinalIgnoreCase) && BigInteger.TryParse(argument.Substring(1), out BigInteger number))
			{
				if (number <= 1)
					await message.Channel.SendMessageAsync("Try a number greater than 1, baka!");
				else
				{
					var bytes = number.ToByteArray();
					BigInteger randNum;
					do
					{
						rand.NextBytes(bytes);
						randNum = new BigInteger(bytes);
					} while (randNum >= number || randNum.Sign == -1);
					await message.Channel.SendMessageAsync($"Rolling a {number} sided :game_die:...\nRolled a {(randNum + 1).ToString()}.");
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		[Command("choose", Help = "Choose one option from a list of choices.")]
		public async Task ChooseCommand(SocketMessage message, [CustomUsageText("<choice> | <choice> | ...")][ListSeparator("|")]string[] arguments)
		{
			if (arguments.Length == 1)
			{
				await message.Channel.SendMessageAsync("I have to choose from a single option... That's difficult...");
				await Task.Delay(TimeSpan.FromSeconds(10));
				await message.Channel.SendMessageAsync($"I've chosen! I pick **{arguments[0]}**.");
			}
			else
			{
				int idx = rand.Next(arguments.Length);
				await message.Channel.SendMessageAsync($"I pick **{arguments[idx]}**.");
			}
		}

		[Command("poke", Help = "Poke someone.")]
		public async Task PokeCommand(SocketMessage message, [Optional]SocketUser user)
		{
			user = user ?? message.Author;
			await message.Channel.SendMessageAsync($"*Baka-chan pokes {user.Mention}*", false,
				imageService.GetImageEmbed("poke"));
		}

		[Command("slap", Help = "Slap someone.")]
		public async Task SlapCommand(SocketMessage message, [Optional]SocketUser user)
		{
			user = user ?? message.Author;
			await message.Channel.SendMessageAsync($"*Baka-chan slaps {user.Mention}*", false, imageService.GetImageEmbed("slap"));
		}

		[Command("f", Help = "Pay respects.")]
		public async Task FCommand(SocketMessage message)
		{
			await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("respect"));
		}

		[Command("disgust")]
		public async Task DisgustCommand(SocketMessage message)
		{
			await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("disgust"));
		}

		[Command("notwork")]
		public async Task NotworkCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("notwork")); }

		[Command("trustme")]
		public async Task TrustmeCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("trustme")); }

		[Command("calmdown")]
		public async Task CalmDownCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("calmdown")); }

		[Command("coverup")]
		public async Task CoverUpCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("cover_up")); }

		[Command("thumbsup")]
		public async Task ThumbsUpCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("thumbs_up")); }

		[Command("gtfo")]
		public async Task GTFOCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("gtfo")); }

		[Command("lewd")]
		public async Task LewdCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("lewd")); }

		[Command("wtf")]
		public async Task WTFCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("wtf")); }

		[Command("themoreyouknow")]
		public async Task TheMoreYouKnowCommand(SocketMessage message) { await message.Channel.SendMessageAsync("", false, imageService.GetImageEmbed("themoreyouknow")); }
	}
}
