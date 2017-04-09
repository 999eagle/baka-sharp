using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;
using SteamWebAPI2.Interfaces;

namespace BakaCore.Commands
{
	class SteamCommands
	{
		ISteamUser steamUser;
		ISteamUserStats steamUserStats;
		ILogger logger;
		public SteamCommands(IServiceProvider services)
		{
			steamUser = services.GetRequiredService<ISteamUser>();
			steamUserStats = services.GetRequiredService<ISteamUserStats>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SteamCommands>();
		}

		[Command("steam", Subcommand = "info", Help = "Displays information about a Steam user.")]
		public async Task SteamCommand(SocketMessage message, string steamID)
		{
			if (steamID.Length != 17 || !UInt64.TryParse(steamID, out ulong userID))
			{
				userID = (await steamUser.ResolveVanityUrlAsync(steamID)).Data;
				logger.LogDebug($"SteamID {steamID} resolved to UserID {userID}.");
			}
			var summary = (await steamUser.GetPlayerSummaryAsync(userID)).Data;
			var text = $"**Profile Name:** {summary.Nickname}\n**Steam ID:** {summary.SteamId}\n**URL:** {summary.ProfileUrl}\n**Status:** {summary.UserStatus}\n**Calculator:** http://steamdb.info/calculator/{summary.SteamId}/";
			await message.Channel.SendMessageAsync(text);
		}
	}
}
