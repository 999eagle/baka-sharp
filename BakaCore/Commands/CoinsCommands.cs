using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using BakaCore.Data;
using Discord.WebSocket;

namespace BakaCore.Commands
{
	class CoinsCommands
	{
		private IDataStore dataStore;
		private Configuration config;
		private ILogger logger;

		public CoinsCommands(IServiceProvider services)
		{
			dataStore = services.GetRequiredService<IDataStore>();
			config = services.GetRequiredService<Configuration>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<CoinsCommands>();
		}

		public (MethodInfo meth, ICommandDescription description)[] GetCustomCommands()
		{
			var typeInfo = GetType().GetTypeInfo();
			return new(MethodInfo, ICommandDescription)[] {
				(typeInfo.GetMethod(nameof(GetCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Help = $"Shows how many {config.Currency.CurrencyName} you or another user has." }),
				(typeInfo.GetMethod(nameof(SpawnCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Subcommand = "spawn", Help = $"Spawns {config.Currency.CurrencyName} on a user." })
			};
		}
		
		public async Task GetCoinsCommand(SocketMessage message, [Optional]SocketUser user)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				var data = dataStore.GetGuildData(channel.Guild);
				if (user == null)
					user = message.Author;
				await channel.SendMessageAsync($"{user.Mention} has {data.GetCoins(user)} {config.Currency.CurrencyName}");
			}
		}

		public async Task SpawnCoinsCommand(SocketMessage message, SocketUser user, int amount)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				if (amount <= 0)
				{
					await channel.SendMessageAsync($"Only a positive amount of {config.Currency.CurrencyName} can be spawned.");
					return;
				}
				var data = dataStore.GetGuildData(channel.Guild);
				int coins = data.GetCoins(user) + amount;
				data.SetCoins(user, coins);
				logger.LogDebug($"[Spawn] Set coins of user {user.Id} in guild {channel.Guild.Id} to {coins}.");
				await channel.SendMessageAsync($"{user.Mention} now has {coins} {config.Currency.CurrencyName}");
			}
		}
	}
}
