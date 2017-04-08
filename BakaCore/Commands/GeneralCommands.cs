using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

namespace BakaCore.Commands
{
	class GeneralCommands
	{
		[Command("info", Help = "Shows information about a user.")]
		public async Task InfoCommand(SocketMessage message, SocketUser user)
		{
			var text = $"Information about {user.Mention}\n**Username:** {user.Username}#{user.Discriminator}\n**Created:** {user.CreatedAt}\n**Avatar URL:**{user.GetAvatarUrl()}";
			await message.Channel.SendMessageAsync(text);
		}
	}
}
