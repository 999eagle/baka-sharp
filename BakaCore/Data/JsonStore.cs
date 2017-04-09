using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

		private Task saveCoinsTask = null;
		private CancellationTokenSource scheduledTaskCancelWait = new CancellationTokenSource();

		public JsonStore(Configuration config)
		{
			this.config = config;
		}

		~JsonStore()
		{
			scheduledTaskCancelWait.Cancel();
			if (saveCoinsTask != null)
			{
				saveCoinsTask.Wait();
			}
		}

		private GuildData CreateNewGuildData(ulong guildId)
		{
			var data = new GuildData(config, guildId);
			data.CoinsChanged += GuildDataCoinsChanged;
			return data;
		}

		private void GuildDataCoinsChanged(GuildData guild, ulong userId)
		{
			ScheduleSaveCoins();
		}

		private void ScheduleSaveCoins()
		{
			if (saveCoinsTask != null && saveCoinsTask.IsCompleted)
			{
				saveCoinsTask.GetAwaiter().GetResult();
				saveCoinsTask = null;
			}

			if (saveCoinsTask == null)
			{
				saveCoinsTask = SaveCoinsTask();
			}

			async Task SaveCoinsTask()
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(30), scheduledTaskCancelWait.Token);
				}
				catch (TaskCanceledException) { }
				using (var file = File.OpenWrite(".\\data\\coins.json"))
				using (var writer = new StreamWriter(file))
				{
					var jObj = new JObject();
					foreach (var kv in guildData)
					{
						var guildObj = new JObject();
						foreach (var kv2 in kv.Value.GetCoinData())
						{
							guildObj.Add(kv2.Key.ToString(), kv2.Value);
						}
						jObj.Add(kv.Key.ToString(), guildObj);
						kv.Value.IsDirty = false;
					}
					await writer.WriteAsync(await Task.Factory.StartNew(() => JsonConvert.SerializeObject(jObj)));
					await writer.FlushAsync();
				}
			}
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
							data.Add(guildId, CreateNewGuildData(guildId));
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
				guildData.Add(guild.Id, CreateNewGuildData(guild.Id));
			return guildData[guild.Id];
		}
	}
}
