using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BakaCore.Data
{
	class JsonStore : IDataStore, IDisposable
	{
		private Configuration config;
		private IDictionary<ulong, GuildData> guildData = null;
		private ILogger logger;

		private Task saveCoinsTask = null;
		private Task saveSettingsTask = null;
		private CancellationTokenSource scheduledTaskCancelWait = new CancellationTokenSource();

		public JsonStore(Configuration config, ILoggerFactory loggerFactory)
		{
			this.config = config;
			logger = loggerFactory.CreateLogger<JsonStore>();
		}

		public void Dispose()
		{
			scheduledTaskCancelWait.Cancel();
			if (saveCoinsTask != null)
			{
				saveCoinsTask.Wait();
			}
			if (saveSettingsTask != null)
			{
				saveSettingsTask.Wait();
			}
		}

		private GuildData CreateNewGuildData(ulong guildId, bool setDefaults)
		{
			var data = new GuildData(config, guildId, setDefaults);
			data.CoinsChanged += GuildDataCoinsChanged;
			data.SettingsChanged += GuildDataSettingsChanged;
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
				logger.LogInformation("Scheduled saving coins.");
			}

			async Task SaveCoinsTask()
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(30), scheduledTaskCancelWait.Token);
				}
				catch (TaskCanceledException) { }
				using (var file = File.Open(".\\data\\coins.json", FileMode.Create, FileAccess.Write))
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
				logger.LogInformation("Saved coins data.");
			}
		}
		private void GuildDataSettingsChanged(GuildData guild)
		{
			ScheduleSaveSettings();
		}

		private void ScheduleSaveSettings()
		{
			if (saveSettingsTask != null && saveSettingsTask.IsCompleted)
			{
				saveSettingsTask.GetAwaiter().GetResult();
				saveSettingsTask = null;
			}

			if (saveSettingsTask == null)
			{
				saveSettingsTask = SaveSettingsTask();
				logger.LogInformation("Scheduled saving settings.");
			}

			async Task SaveSettingsTask()
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(30), scheduledTaskCancelWait.Token);
				}
				catch (TaskCanceledException) { }
				using (var file = File.Open(".\\data\\settings.json", FileMode.Create, FileAccess.Write))
				using (var writer = new StreamWriter(file))
				{
					var jObj = new JObject();
					foreach (var kv in guildData)
					{
						var guildObj = new JObject();
						foreach (var kv2 in kv.Value.GetSettingsData())
						{
							guildObj.Add(kv2.Key, kv2.Value);
						}
						jObj.Add(kv.Key.ToString(), guildObj);
						kv.Value.IsDirty = false;
					}
					await writer.WriteAsync(await Task.Factory.StartNew(() => JsonConvert.SerializeObject(jObj)));
					await writer.FlushAsync();
				}
				logger.LogInformation("Saved settings data.");
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
							data.Add(guildId, CreateNewGuildData(guildId, true));
						var guild = data[guildId];
						guild.SetCoinData(((JObject)property.Value).Properties().ToDictionary(p => UInt64.Parse(p.Name), p => (int)p.Value));
					}
				}
			}
			if (File.Exists(".\\data\\settings.json"))
			{
				using (var file = File.OpenText(".\\data\\settings.json"))
				using (var reader = new JsonTextReader(file))
				{
					var settings = (JObject)JToken.ReadFrom(reader);
					foreach (var property in settings.Properties())
					{
						var guildId = UInt64.Parse(property.Name);
						if (!data.ContainsKey(guildId))
							data.Add(guildId, CreateNewGuildData(guildId, true));
						var guild = data[guildId];
						guild.SetSettingsData(((JObject)property.Value).Properties().ToDictionary(p => p.Name, p => (string)p.Value));
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
				guildData.Add(guild.Id, CreateNewGuildData(guild.Id, true));
			return guildData[guild.Id];
		}
	}
}
