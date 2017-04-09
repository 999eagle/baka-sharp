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
		}
		public class API
		{
			public string DiscordLoginToken { get; set; }
			public string SteamWebAPIKey { get; set; }
		}
		public class Currency
		{
			public int StartCurrency { get; set; }
			public string CurrencyName { get; set; }
			public string CurrencyCommand { get; set; }
		}
	}
	public class Configuration
	{
		public Logging Logging { get; set; }
		public ConfigClasses.Commands Commands { get; set; }
		public API API { get; set; }
		public Currency Currency { get; set; }
	}
}
