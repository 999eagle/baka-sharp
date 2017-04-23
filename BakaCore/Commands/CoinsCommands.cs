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
				(typeInfo.GetDeclaredMethod(nameof(GetCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Help = $"Shows how many {config.Currency.CurrencyName} you or another user has.", Scope = CommandScope.Guild }),
				(typeInfo.GetDeclaredMethod(nameof(SpawnCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Subcommand = "spawn", Help = $"Spawns {config.Currency.CurrencyName} on a user.", RequiredPermissions = Permissions.SpawnCoins, Scope = CommandScope.Guild }),
				(typeInfo.GetDeclaredMethod(nameof(DespawnCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Subcommand = "despawn", Help = $"Despawns {config.Currency.CurrencyName} from a user.", RequiredPermissions = Permissions.DespawnCoins, Scope = CommandScope.Guild }),
				(typeInfo.GetDeclaredMethod(nameof(GiveCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Subcommand = "give", Help = $"Give {config.Currency.CurrencyName} to someone else.", Scope = CommandScope.Guild })
			};
		}
		
		public async Task GetCoinsCommand(SocketMessage message, [Optional]SocketUser user)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				var data = dataStore.GetGuildData(channel.Guild);
				if (user == null)
					user = message.Author;
				await channel.SendMessageAsync($"{user.Mention} has {data.GetCoins(user)} {config.Currency.CurrencyName}.");
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
				logger.LogTrace($"[Spawn] Set coins of user {user.Id} in guild {channel.Guild.Id} to {coins}.");
				await channel.SendMessageAsync($"{user.Mention} now has {coins} {config.Currency.CurrencyName}.");
			}
		}

		public async Task DespawnCoinsCommand(SocketMessage message, SocketUser user, int amount)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				if (amount <= 0)
				{
					await channel.SendMessageAsync($"Only a positive amount of {config.Currency.CurrencyName} can be despawned.");
					return;
				}
				var data = dataStore.GetGuildData(channel.Guild);
				int coins = data.GetCoins(user);
				if (coins < amount)
				{
					await channel.SendMessageAsync($"{user.Mention} has only {coins} {config.Currency.CurrencyName}.");
					return;
				}
				coins -= amount;
				data.SetCoins(user, coins);
				logger.LogTrace($"[Despawn] Set coins of user {user.Id} in guild {channel.Guild.Id} to {coins}.");
				await channel.SendMessageAsync($"{user.Mention} now has {coins} {config.Currency.CurrencyName}.");
			}
		}

		public async Task GiveCoinsCommand(SocketMessage message, SocketUser user, int amount)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				if (message.Author.Id == user.Id)
				{
					await channel.SendMessageAsync($"No need to give your own {config.Currency.CurrencyName} to yourself.");
					return;
				}
				if (amount <= 0)
				{
					await channel.SendMessageAsync($"{message.Author.Mention} wanted to steal {config.Currency.CurrencyName} from {user.Mention}!");
					return;
				}
				var data = dataStore.GetGuildData(channel.Guild);
				int coinsReceiver = data.GetCoins(user);
				int coinsSender = data.GetCoins(message.Author);
				if (coinsSender < amount)
				{
					await channel.SendMessageAsync($"{message.Author.Mention} has only {coinsSender} {config.Currency.CurrencyName}.");
					return;
				}
				coinsSender -= amount;
				coinsReceiver += amount;
				data.SetCoins(user, coinsReceiver);
				logger.LogTrace($"[Give] Set coins of user {user.Id} in guild {channel.Guild.Id} to {coinsReceiver}.");
				data.SetCoins(message.Author, coinsSender);
				logger.LogTrace($"[Give] Set coins of user {message.Author.Id} in guild {channel.Guild.Id} to {coinsSender}.");
				await channel.SendMessageAsync($"{user.Mention} received {amount} {config.Currency.CurrencyName} from {message.Author.Mention} and now has {coinsReceiver} {config.Currency.CurrencyName}.");
			}
		}
	}
}
