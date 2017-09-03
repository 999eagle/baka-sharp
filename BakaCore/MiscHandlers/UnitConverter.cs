using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;
using Units;

namespace BakaCore.MiscHandlers
{
	class UnitConverter
	{
		private ILogger logger;
		private DiscordSocketClient client;

		private static string numberRegex = "(-?(([0-9]+(\\.[0-9]*)?)|(\\.[0-9]+)))";
		private static IEnumerable<(string groupName, string regex, string fromUnit, string toUnit)> knownUnitRegexes = new[]
		{
			("ft", "ft|'", null, "m"),
			("F", "°?[Ff]", "°F", "°C"),
			("mph", "mph", null, "km/h"),
			("in", "in|''|\"", null, "cm"),
		};
		private static string knownUnitRegex = String.Join("|", knownUnitRegexes.Select(t => $"(?<{t.groupName}>{t.regex})"));
		private static string simpleValueRegex = $"((?<value>{numberRegex}) ?(?<unit>{knownUnitRegex}))";
		private static string ftinValueRegex = $"((?<value_ft>{numberRegex}) ?(ft|') ?(?<value_in>{numberRegex})( ?(in|\"|''))?)";
		private Regex unitRegex = new Regex($"{ftinValueRegex}|{simpleValueRegex}");

		public UnitConverter(ILoggerFactory loggerFactory, DiscordSocketClient client)
		{
			this.logger = loggerFactory.CreateLogger<UnitConverter>();
			this.client = client;
			this.client.MessageReceived += MessageReceived;

			logger.LogInformation("Initialized");
		}

		private async Task MessageReceived(SocketMessage message)
		{
			if (message.Author.Id == client.CurrentUser.Id) return;
			var matches = unitRegex.Matches(message.Content);
			string result = "";
			foreach (Match match in matches)
			{
				if (match.Groups["value"].Success)
				{
					if (!Double.TryParse(match.Groups["value"].Value, out var value)) continue;
					var fromUnit = "";
					var toUnit = "";
					foreach (var unit in knownUnitRegexes)
					{
						if (match.Groups[unit.groupName].Success)
						{
							(fromUnit, toUnit) = (unit.fromUnit ?? unit.groupName, unit.toUnit);
							break;
						}
					}
					if (fromUnit != "")
					{
						var m = new Measurement(value, fromUnit);
						result += $"{m} = {m.ConvertTo(toUnit)}\n";
					}
				}
				else if (match.Groups["value_ft"].Success && match.Groups["value_in"].Success)
				{
					if (!Double.TryParse(match.Groups["value_ft"].Value, out var valueFt)) continue;
					if (!Double.TryParse(match.Groups["value_in"].Value, out var valueIn)) continue;
					var f = new Measurement(valueFt, "ft");
					var i = new Measurement(valueIn, "in");
					result += $"{f} {i} = {(f + i).ConvertTo("m")}\n";
				}
			}
			if (result != "")
			{
				await message.Channel.SendMessageAsync($"I've detected the use of some non-standard freedom units in your message!\nHere's the converted data:\n{result}");
			}
		}
	}
}
