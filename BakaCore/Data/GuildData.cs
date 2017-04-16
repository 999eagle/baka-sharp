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
		private IDictionary<string, string> settings;
		private IDictionary<ulong, Permissions> permissions;
		private Configuration config;

		public ulong GuildId { get; }
		public bool IsDirty { get; set; }
		public event Action<GuildData, ulong> CoinsChanged;
		public event Action<GuildData> SettingsChanged;
		public event Action<GuildData, ulong> PermissionsChanged;

		public GuildData(Configuration config, ulong guildId, bool setDefaults)
		{
			this.config = config;
			GuildId = guildId;
			if (setDefaults)
			{
				coins = new Dictionary<ulong, int>();
				settings = new Dictionary<string, string>()
				{
					{ "welcomeChannel", "" },
					{ "strike_3", "kick" },
					{ "strike_4", "mutetext,mutevoice" },
					{ "strike_5", "ban" }
				};
				permissions = new Dictionary<ulong, Permissions>();
			}
		}

		public IDictionary<ulong, int> GetCoinData()
		{
			return coins;
		}

		public IDictionary<string, string> GetSettingsData() => settings;
		public IDictionary<ulong, Permissions> GetPermissionsData() => permissions;

		public void SetCoinData(IDictionary<ulong, int> coins)
		{
			this.coins = coins;
		}

		public void SetSettingsData(IDictionary<string, string> settings)
		{
			this.settings = settings;
		}
		public void SetPermissionsData(IDictionary<ulong, Permissions> permissions) { this.permissions = permissions; }

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
			CoinsChanged?.Invoke(this, user.Id);
		}

		public string GetStrikeAction(int strikes)
		{
			if (settings.TryGetValue($"strike_{strikes}", out var action))
				return action;
			else
				return null;
		}
		public void SetStrikeAction(int strikes, string action)
		{
			if (strikes > 7) return;
			settings[$"strike_{strikes}"] = action;
			SettingsChanged?.Invoke(this);
		}
		public ulong GetWelcomeChannel()
		{
			if (settings.TryGetValue("welcomeChannel", out var channel) && UInt64.TryParse(channel, out var channelId))
				return channelId;
			else
				return 0;
		}
		public void SetWelcomeChannel(ulong channel)
		{
			settings["welcomeChannel"] = channel.ToString();
			SettingsChanged?.Invoke(this);
		}

		public Permissions GetPermissions(SocketEntity<ulong> entity)
		{
			if (permissions.ContainsKey(entity.Id))
				return permissions[entity.Id];
			return Permissions.None;
		}

		public void SetPermissions(SocketEntity<ulong> entity, Permissions permissions)
		{
			if (!(entity is SocketUser) && !(entity is SocketRole)) throw new ArgumentException("entity");
			this.permissions[entity.Id] = permissions;
			IsDirty = true;
			PermissionsChanged?.Invoke(this, entity.Id);
		}
	}
}
