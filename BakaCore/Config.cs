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
		}
		public class API
		{
			public string DiscordLoginToken { get; set; }
			public string SteamWebAPIKey { get; set; }
		}
	}
	public class Configuration
	{
		public Logging Logging { get; set; }
		public ConfigClasses.Commands Commands { get; set; }
		public API API { get; set; }
	}
}
