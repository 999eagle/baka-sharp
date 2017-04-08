using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using SteamWebAPI2.Interfaces;

namespace BakaCore.Commands
{
	class SteamCommands
	{
		ISteamUser steamUser;
		ISteamUserStats steamUserStats;
		public SteamCommands(IServiceProvider services)
		{
			steamUser = services.GetRequiredService<ISteamUser>();
			steamUserStats = services.GetRequiredService<ISteamUserStats>();
		}

		[Command("steam", Subcommand = "info", Help = "Displays information about a Steam user.")]
		public async Task SteamCommand(SocketMessage message, string steamID)
		{
			ulong userID = 0;
			if (steamID.Length != 17 || !UInt64.TryParse(steamID, out userID))
			{
				userID = (await steamUser.ResolveVanityUrlAsync(steamID)).Data;
			}
			var summary = (await steamUser.GetPlayerSummaryAsync(userID)).Data;
			var text = $"**Profile Name:** {summary.Nickname}\n**Steam ID:** {summary.SteamId}\n**URL:** {summary.ProfileUrl}\n**Status:** {summary.UserStatus}\n**Calculator:** http://steamdb.info/calculator/{summary.SteamId}/";
			await message.Channel.SendMessageAsync(text);
		}
	}
}
