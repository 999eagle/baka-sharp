using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;
using BakaCore.Data;

namespace BakaCore.Commands
{
	class GameCommands
	{
		ILogger logger;
		Configuration config;
		IDataStore dataStore;
		Random rand;

		public GameCommands(IServiceProvider services)
		{
			dataStore = services.GetRequiredService<IDataStore>();
			config = services.GetRequiredService<Configuration>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<GameCommands>();
			rand = services.GetRequiredService<Random>();

			foreach (var win in config.Commands.Slots.Wins)
			{
				win.Combination = (win.Combination?.Where(i => i > 1 && i <= config.Commands.Slots.Count).OrderBy(i => i).ToArray()) ?? new int[0];
			}
			// dictionary that maps item names to their index
			var itemIndices = Enumerable.Range(0, config.Commands.Slots.Items.Length).ToDictionary(i => config.Commands.Slots.Items[i]);
			// store bonuses using their item index instead of their name
			config.Commands.Slots.InternalBonuses = config.Commands.Slots.Bonuses.ToDictionary(kv => itemIndices[kv.Key], kv => kv.Value);
		}

		[Command("slots", Help = "Play a round of slots.", IsGuildOnly = true)]
		public async Task SlotsCommand(SocketMessage message, int bet)
		{
			if (message.Channel is SocketTextChannel channel)
			{
				if (bet <= 0)
				{
					await channel.SendMessageAsync($"You have to bet at least 1 {config.Currency.CurrencyName}.");
					return;
				}
				var data = dataStore.GetGuildData(channel.Guild);
				int coins = data.GetCoins(message.Author);
				if (coins < bet)
				{
					await channel.SendMessageAsync($"You only have {coins} {config.Currency.CurrencyName}.");
					return;
				}
				var text = $":slot_machine: Spending {bet} {config.Currency.CurrencyName}\nResults:";
				var chosenItems = new List<int>();
				for (int i = 0; i < config.Commands.Slots.Count; i++)
				{
					var idx = rand.Next(config.Commands.Slots.Items.Length);
					text += $":{config.Commands.Slots.Items[idx]}: ";
					chosenItems.Add(idx);
				}
				// for each n matching items (with n > 1) contains n
				// examples: a pair will be [2], two pairs will be [2,2], a pair and a triple will be [2,3]
				var counts = chosenItems.GroupBy(i => i).Select(g => g.Count()).Where(i => i > 1).OrderBy(i => i);
				// get the first win where the combination is equal to the one in counts
				var win = config.Commands.Slots.Wins.FirstOrDefault(w => counts.SequenceEqual(w.Combination));
				if (win == null)
				{
					logger.LogWarning($"No win description found for combination [{String.Join(",", counts)}], falling back to empty combination.");
					win = config.Commands.Slots.Wins.FirstOrDefault(w => !w.Combination.Any());
					if (win == null)
					{
						logger.LogWarning($"No win description found for empty combination, falling back to built-in defaults.");
						win = new ConfigClasses.Commands._Slots._Win()
						{
							Payout = 0,
							Text = "You won nothing."
						};
					}
				}
				text += $"\n{win.Text}";
				int payout = win.Payout;
				if (payout > 0)
				{
					var bonuses = chosenItems
						.Where(config.Commands.Slots.InternalBonuses.ContainsKey)
						.GroupBy(i => i)
						.Select(g => (item: g.Key, count: g.Count(), bonusText: config.Commands.Slots.InternalBonuses[g.Key]));
					if (bonuses.Any())
					{
						text += " You got these bonuses: " + String.Join(", ",
							bonuses.Select(t => $":{config.Commands.Slots.Items[t.item]}: {t.bonusText}{(t.count > 1 ? $" x{t.count}" : "")}")) + ".";
						payout *= (int)Math.Pow(2, bonuses.Sum(t => t.count));
					}
				}
				coins += (payout - 1) * bet;
				if (payout > 0)
				{
					text += $"\nYou won {payout}x your bet for a total of {bet * payout} {config.Currency.CurrencyName} and now have {coins} {config.Currency.CurrencyName}.";
				}
				data.SetCoins(message.Author, coins);
				logger.LogTrace($"[Slots] Set coins of user {message.Author.Id} in guild {channel.Guild.Id} to {coins}.");
				await channel.SendMessageAsync(text);
			}
		}
	}
}
