using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Exceptions;
using SteamWebAPI2.Utilities;

namespace BakaCore.Commands
{
	class SteamCommands
	{
		ISteamUser steamUser;
		ISteamUserStats steamUserStats;
		ILogger logger;
		public SteamCommands(IServiceProvider services)
		{
			var config = services.GetRequiredService<Configuration>();
			steamUser = new SteamUser(config.API.SteamWebAPIKey);
			steamUserStats = new SteamUserStats(config.API.SteamWebAPIKey);
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<SteamCommands>();
		}

		[Command("steam", Subcommand = "info", Help = "Displays information about a Steam user.")]
		public async Task SteamCommand(SocketMessage message, string steamID)
		{
			var summary = await GetSteamAPIData(steamID, steamUser.GetPlayerSummaryAsync);
			if (summary == null)
			{
				await message.Channel.SendMessageAsync("No Steam account matching that ID found.");
				return;
			}
			var text = $"**Profile Name:** {summary.Nickname}\n**Steam ID:** {summary.SteamId}\n**URL:** {summary.ProfileUrl}\n**Status:** {summary.UserStatus}\n**Calculator:** http://steamdb.info/calculator/{summary.SteamId}/";
			await message.Channel.SendMessageAsync(text);
		}

		[Command("csgo", Subcommand = "stats", Help = "Displays a summary of a player's CS:GO stats.")]
		public async Task CsgsoStatsCommand(SocketMessage message, string steamID)
		{
			try
			{
				var stats = await GetSteamAPIData(steamID, (u) => steamUserStats.GetUserStatsForGameAsync(u, 730));
				if (stats == null)
				{
					await message.Channel.SendMessageAsync("No Steam account matching that ID found.");
					return;
				}
				var statsDict = stats.Stats.ToDictionary(u => u.Name, u => u.Value);
				var text = $"**Total stats**\nKills: {statsDict["total_kills"]}, Deaths: {statsDict["total_deaths"]}, K/D: {(double)statsDict["total_kills"] / statsDict["total_deaths"] :0.###}, Headshot-Kills: {statsDict["total_kills_headshot"]}, Accuracy: {(double)statsDict["total_shots_hit"] / statsDict["total_shots_fired"]:0.####%}, Total MVPs: {statsDict["total_mvps"]}, Bombs defused: {statsDict["total_defused_bombs"]}, Bombs planted: {statsDict["total_planted_bombs"]}\n\n" +
					$"**Last Match**\nKills: {statsDict["last_match_kills"]}, Deaths: {statsDict["last_match_deaths"]}, K/D: {(double)statsDict["last_match_kills"] / statsDict["last_match_deaths"] :0.###}, MVPs: {statsDict["last_match_mvps"]}, Favourite weapon: {(csgoWeapons.TryGetValue(statsDict["last_match_favweapon_id"], out var weapon) ? weapon : $"unknown weapon (id: {statsDict["last_match_favweapon_id"]})")}";
				await message.Channel.SendMessageAsync(text);
			}
			catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("400 (Bad Request)"))
			{
				await message.Channel.SendMessageAsync("No stats available for this Steam account.");
				return;
			}
		}

		private async Task<T> GetSteamAPIData<T>(string steamID, Func<ulong, Task<ISteamWebResponse<T>>> steamApiCall) where T : class
		{
			(bool success, ulong userID) = await TryParseUserId(steamID);
			if (!success)
			{
				return null;
			}
			return (await steamApiCall(userID))?.Data;
		}

		private async Task<(bool success, ulong userID)> TryParseUserId(string steamID)
		{
			if (steamID.Length != 17 || !UInt64.TryParse(steamID, out ulong userID))
			{
				try
				{
					userID = (await steamUser.ResolveVanityUrlAsync(steamID)).Data;
					logger.LogDebug($"SteamID {steamID} resolved to UserID {userID}.");
				}
				catch (VanityUrlNotResolvedException)
				{
					return (false, 0);
				}
			}
			return (true, userID);
		}

		// source: https://tf2b.com/itemlist.php?gid=730
		static IDictionary<uint, string> csgoWeapons = new Dictionary<uint, string>
		{
			{ 01, "Desert Eagle" },
			{ 02, "Dual Berettas" },
			{ 03, "Five-SeveN" },
			{ 04, "Glock-18" },
			{ 07, "AK-47" },
			{ 08, "AUG" },
			{ 09, "AWP" },
			{ 10, "FAMAS" },
			{ 11, "G3SG1" },
			{ 13, "Galil AR" },
			{ 14, "M249" },
			{ 16, "M4A1" },
			{ 17, "MAC10" },
			{ 19, "P90" },
			{ 24, "UMP-45" },
			{ 25, "XM1014" },
			{ 26, "PP-Bizon" },
			{ 27, "MAG-7" },
			{ 28, "Negev" },
			{ 29, "Sawed-Off" },
			{ 30, "Tec-9" },
			{ 31, "Zeus x27" },
			{ 32, "P2000" },
			{ 33, "MP7" },
			{ 34, "MP9" },
			{ 35, "Nova" },
			{ 36, "P250" },
			{ 38, "SCAR-20" },
			{ 39, "SG 553" },
			{ 40, "SSG 08" },
			{ 42, "Knife" },
			{ 43, "Flashbang" },
			{ 44, "HE Grenade" },
			{ 45, "Smoke Grenade" },
			{ 46, "Molotov" },
			{ 47, "Decoy Grenade" },
			{ 48, "Incendiary Grenade" },
			{ 49, "C4 Explosive" },
			{ 59, "Knife" },
			{ 60, "M4A1-S" },
			{ 61, "USP-S" },
			{ 63, "CZ75-Auto" },
			{ 64, "R8 Revolver" },
			{ 500, "Bayonet" },
			{ 505, "Flip Knife" },
			{ 506, "Gut Knife" },
			{ 507, "Garambit" },
			{ 508, "M9 Bayonet" },
			{ 509, "Huntsman Knife" },
			{ 512, "Falchion Knife" },
			{ 514, "Bowie Knife" },
			{ 515, "Butterfly Knife" },
			{ 516, "Shadow Daggers" },
		};
	}
}
