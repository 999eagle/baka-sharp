using System;
using System.Collections.Generic;
using System.Text;

using Discord;
using Discord.WebSocket;

namespace BakaCore.Data
{
	class GuildData
	{
		private IDictionary<ulong, int> coins;
		private Configuration config;

		public ulong GuildId { get; }
		public bool IsDirty { get; set; }
		public event Action<GuildData, string> DataChanged;

		public GuildData(Configuration config, ulong guildId)
		{
			this.config = config;
			GuildId = guildId;
		}

		public void SetCoinData(IDictionary<ulong, int> coins)
		{
			this.coins = coins;
		}

		public int GetCoins(SocketUser user)
		{
			if (coins.ContainsKey(user.Id))
				return coins[user.Id];
			return config.Currency.StartCurrency;
		}

		public void SetCoins(SocketUser user, int coins)
		{
			if (this.coins.ContainsKey(user.Id))
				this.coins[user.Id] = coins;
			else
				this.coins.Add(user.Id, coins);
			IsDirty = true;
			DataChanged?.Invoke(this, nameof(this.coins));
		}
	}
}
