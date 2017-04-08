using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BakaCore.Data
{
	class JsonStore : IDataStore
	{
		private Configuration config;
		private IDictionary<ulong, GuildData> guildData = null;

		public JsonStore(Configuration config)
		{
			this.config = config;
		}

		private void LoadData()
		{
			var data = new Dictionary<ulong, GuildData>();

			if (File.Exists(".\\data\\coins.json"))
			{
				using (var file = File.OpenText(".\\data\\coins.json"))
				using (var reader = new JsonTextReader(file))
				{
					var coins = (JObject)JToken.ReadFrom(reader);
					foreach (var property in coins.Properties())
					{
						var guildId = UInt64.Parse(property.Name);
						if (!data.ContainsKey(guildId))
							data.Add(guildId, new GuildData(config, guildId));
						var guild = data[guildId];
						guild.SetCoinData(((JObject)property.Value).Properties().ToDictionary(p => UInt64.Parse(p.Name), p => (int)p.Value));
					}
				}
			}
			guildData = data;
		}

		public GuildData GetGuildData(SocketGuild guild)
		{
			if (guildData == null)
				LoadData();
			if (!guildData.ContainsKey(guild.Id))
				guildData.Add(guild.Id, new GuildData(config, guild.Id));
			return guildData[guild.Id];
		}
	}
}
