using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using BakaCore.Data;
using Discord.WebSocket;

namespace BakaCore.Commands
{
	class CoinsCommands
	{
		private IDataStore dataStore;
		private Configuration config;

		public CoinsCommands(IServiceProvider services)
		{
			dataStore = services.GetRequiredService<IDataStore>();
			config = services.GetRequiredService<Configuration>();
		}

		public (MethodInfo meth, ICommandDescription description)[] GetCustomCommands()
		{
			var typeInfo = GetType().GetTypeInfo();
			return new (MethodInfo, ICommandDescription)[] {
				(typeInfo.GetMethod(nameof(GetCoinsCommand)), new CommandDescription(config.Currency.CurrencyCommand) { Help = $"Shows how many {config.Currency.CurrencyName} you or another user has." })
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
	}
}
