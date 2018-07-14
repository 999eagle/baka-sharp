using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using BakaCore.ConfigClasses;

namespace BakaCore
{
	namespace ConfigClasses
	{
		public class Logging
		{
			public ILoggerFactory LoggerFactory { get; set; }
			public LogLevel LogLevel { get; set; }
		}
		public class Commands
		{
			public bool Disabled { get; set; }
			public string Tag { get; set; }
			public _Slots Slots { get; set; }
			public _RPS RPS { get; set; }
			public bool EnableUnitAutoConversion { get; set; }
			public class _Slots
			{
				public int Count { get; set; }
				public string[] Items { get; set; }
				public IDictionary<string, string> Bonuses { get; set; }
				internal IDictionary<int, string> InternalBonuses { get; set; }
				public _Win[] Wins { get; set; }

				public class _Win
				{
					public int Payout { get; set; }
					public string Text { get; set; }
					public int[] Combination { get; set; }
				}
			}
			public class _RPS
			{
				public int AcceptTimeout { get; set; }
				public int ChoiceTimeout { get; set; }
			}
		}
		public class API
		{
			public string DiscordLoginToken { get; set; }
			public string SteamWebAPIKey { get; set; }
			public string GoogleAPIKey { get; set; }
		}
		public class Currency
		{
			public int StartCurrency { get; set; }
			public string CurrencyName { get; set; }
			public string CurrencyCommand { get; set; }
		}
		public class Images
		{
			public string BaseURL { get; set; }
			public IDictionary<string, _ImageData> ImageData { get; set; }
			public class _ImageData
			{
				public string FileName { get; set; }
				public int Count { get; set; }
			}
		}
		public class Songs
		{
			public bool Enabled { get; set; }
			public int Timeout { get; set; }
		}
		public class Music
		{
			public TimeSpan MaximumSongLengthTimeSpan { get; private set; }
			public TimeSpan MaximumSongAgeTimeSpan { get; private set; }
			public int EncodingSampleRate { get; set; }
			public int EncodingBitrate { get; set; }
			public string MaximumSongLength
			{
				get => MaximumSongLengthTimeSpan.ToString();
				set => MaximumSongLengthTimeSpan = System.Xml.XmlConvert.ToTimeSpan(value); // XmlConvert enables using ISO8601 time format
			}
			public string MaximumSongAge
			{
				get => MaximumSongAgeTimeSpan.ToString();
				set => MaximumSongAgeTimeSpan = System.Xml.XmlConvert.ToTimeSpan(value);
			}
		}
		public class DataStore
		{
			public string DataPath { get; set; }
		}
	}
	public class Configuration
	{
		public Logging Logging { get; set; }
		public ConfigClasses.Commands Commands { get; set; }
		public API API { get; set; }
		public Currency Currency { get; set; }
		public Images Images { get; set; }
		public ConfigClasses.Songs Songs { get; set; }
		public ConfigClasses.Music Music { get; set; }
		public DataStore DataStore { get; set; }
	}
}
