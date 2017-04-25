using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

		[Command("slots", Help = "Play a round of slots.", Scope = CommandScope.Guild)]
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

		class RpsGame
		{
			public SocketUser Player1 { get; set; }
			public SocketUser Player2 { get; set; }
			public SocketTextChannel Channel { get; set; }
			public CancellationTokenSource TimeoutCancellation { get; } = new CancellationTokenSource();
			public RpsGameStatus Status { get; set; } = RpsGameStatus.New;
		}
		enum RpsGameStatus
		{
			New,
			Player2Accepted,
			ChoicesReceived
		}
		IList<RpsGame> runningGames = new List<RpsGame>();

		[Command("rps", Help = "Challenge a user to a game of rock-paper-scissors.", Scope = CommandScope.Guild)]
		public async Task RpsCommand(SocketMessage message, SocketUser user, int bet, [Optional]string rpsls)
		{
			if (!(message.Channel is SocketTextChannel channel)) return;
			if (message.Author.Id == user.Id)
			{
				await channel.SendMessageAsync("You can't challenge yourself, baka!");
				return;
			}
			if (bet <= 0)
			{
				await channel.SendMessageAsync($"You have to bet more than 0 {config.Currency.CurrencyName}.");
				return;
			}
			var guildData = dataStore.GetGuildData(channel.Guild);
			if (guildData.GetCoins(message.Author) < bet)
			{
				await channel.SendMessageAsync($"You don't have enough {config.Currency.CurrencyName}.");
				return;
			}
			if (guildData.GetCoins(user) < bet)
			{
				await channel.SendMessageAsync($"Your opponent doesn't have enough {config.Currency.CurrencyName}.");
				return;
			}
			if (runningGames.Any(g => g.Player1.Id == message.Author.Id || g.Player2.Id == message.Author.Id))
			{
				await channel.SendMessageAsync("You are already in a game of rock-paper-scissors.");
				return;
			}
			if (runningGames.Any(g => g.Player1.Id == user.Id || g.Player2.Id == user.Id))
			{
				await channel.SendMessageAsync("Your opponent is already in a game of rock-paper-scissors.");
				return;
			}
			var isRpsls = (rpsls == null) ? false : (rpsls == "rpsls");
			var game = new RpsGame { Player1 = message.Author, Player2 = user, Channel = channel };
			runningGames.Add(game);
			await channel.SendMessageAsync($"{user.Mention}, you have been challenged to a game of rock-paper-scissors. You can accept in the next {config.Commands.RPS.AcceptTimeout} seconds with `{config.Commands.Tag}rps a`");
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(config.Commands.RPS.AcceptTimeout), game.TimeoutCancellation.Token);
				await channel.SendMessageAsync($"{user.Mention} didn't accept {message.Author.Mention}'s challenge in time.");
			}
			catch (TaskCanceledException)
			{
				int player1Coins, player2Coins;
				if ((player1Coins = guildData.GetCoins(message.Author)) < bet)
				{
					await channel.SendMessageAsync($"You don't have enough {config.Currency.CurrencyName} anymore.");
					return;
				}
				if ((player2Coins = guildData.GetCoins(user)) < bet)
				{
					await channel.SendMessageAsync($"Your opponent doesn't have enough {config.Currency.CurrencyName} anymore.");
					return;
				}
				guildData.SetCoins(message.Author, player1Coins - bet);
				guildData.SetCoins(user, player2Coins - bet);
				var validChoices = new List<string> { "rock", "paper", "scissors" };
				if (isRpsls)
				{
					validChoices.Add("lizard");
					validChoices.Add("spock");
				}
				var rpsWin = new Dictionary<int, int[]>
				{
					{ 0, new[] { 2, 3 } },
					{ 1, new[] { 0, 4 } },
					{ 2, new[] { 1, 3 } },
					{ 3, new[] { 1, 4 } },
					{ 4, new[] { 0, 2 } }
				};
				var choiceString = String.Join(", ", validChoices.Take(validChoices.Count - 1)) + " or " + validChoices.Last();
				await channel.SendMessageAsync($"{user.Mention} has accepted {message.Author}'s challenge. Both players, please send me your choice ({choiceString}) via DM within the next {config.Commands.RPS.ChoiceTimeout} seconds.");
				var player1Task = channel.Discord.WaitForMessageAsync(TimeSpan.FromSeconds(config.Commands.RPS.ChoiceTimeout), CreateMessageFilter(message.Author));
				var player2Task = channel.Discord.WaitForMessageAsync(TimeSpan.FromSeconds(config.Commands.RPS.ChoiceTimeout), CreateMessageFilter(user));
				var player1Msg = await player1Task;
				var player2Msg = await player2Task;
				if (player1Msg == null || player2Msg == null)
				{
					await channel.SendMessageAsync("I didn't receive your choices in time. The game was canceled.");
					guildData.SetCoins(message.Author, guildData.GetCoins(message.Author) + bet);
					guildData.SetCoins(user, guildData.GetCoins(user) + bet);
					return;
				}
				var p1Choice = validChoices.IndexOf(player1Msg.Content.ToLowerInvariant());
				var p2Choice = validChoices.IndexOf(player2Msg.Content.ToLowerInvariant());
				var text = $"{message.Author.Mention} chose {validChoices[p1Choice]}, {user.Mention} chose {validChoices[p2Choice]}. ";
				if (rpsWin[p1Choice].Contains(p2Choice))
				{
					text += $"That's a win for {message.Author.Mention}!";
					guildData.SetCoins(message.Author, guildData.GetCoins(message.Author) + bet * 2);
				}
				else if (rpsWin[p2Choice].Contains(p1Choice))
				{
					text += $"That's a win for {user.Mention}!";
					guildData.SetCoins(user, guildData.GetCoins(user) + bet * 2);
				}
				else
				{
					text += "That's a draw!";
					guildData.SetCoins(message.Author, guildData.GetCoins(message.Author) + bet);
					guildData.SetCoins(user, guildData.GetCoins(user) + bet);
				}
				await channel.SendMessageAsync(text);

				Func<SocketMessage, bool> CreateMessageFilter(SocketUser targetUser)
				{
					return (msg) => MessageFilter(msg).GetAwaiter().GetResult();
					async Task<bool> MessageFilter(SocketMessage msg)
					{
						if (!(msg.Channel is SocketDMChannel && targetUser.Id == msg.Author.Id))
						{
							return false;
						}
						if (validChoices.Contains(msg.Content.ToLowerInvariant()))
						{
							await msg.Channel.SendMessageAsync($"You chose {msg.Content.ToLowerInvariant()}.");
							return true;
						}
						await msg.Channel.SendMessageAsync($"Please choose one of {choiceString}.");
						return false;
					}
				}
			}
			finally
			{
				runningGames.Remove(game);
			}
		}

		[Command("rps", Subcommand = "a", Scope = CommandScope.Guild)]
		public async Task RpsAcceptCommand(SocketMessage message)
		{
			if (!(message.Channel is SocketTextChannel channel)) return;
			var game = runningGames.FirstOrDefault(g => (g.Player1.Id == message.Author.Id || g.Player2.Id == message.Author.Id) && g.Channel.Id == channel.Id);
			if (game == null)
			{
				await channel.SendMessageAsync($"You haven't been challenged to a game of rock-paper-scissors. Challenge someone using `{config.Commands.Tag}rps`!");
				return;
			}
			if (game.Player1.Id == message.Author.Id)
			{
				await channel.SendMessageAsync("You've started this game yourself, no need to accept.");
				return;
			}
			if (game.Status != RpsGameStatus.New)
			{
				await channel.SendMessageAsync("This game is already running.");
				return;
			}
			game.Status = RpsGameStatus.Player2Accepted;
			game.TimeoutCancellation.Cancel();
		}
	}
}
