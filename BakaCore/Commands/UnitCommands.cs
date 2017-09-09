using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.WebSocket;

using Units;

namespace BakaCore.Commands
{
	class UnitCommands
	{
		public UnitCommands()
		{
		}

		[Command("conv", Help = "Convert nearly arbitrary units of measurement.")]
		public async Task<bool> ConvertMeasurementCommand(SocketMessage message, double value, [CustomUsageText("<from unit> to <to unit>")][ListSeparator("to")]string[] units)
		{
			if (units.Length != 2) { return false; }
			if (!Unit.TryParse(units[0], out var from) || !Unit.TryParse(units[1], out var to))
			{
				await message.Channel.SendMessageAsync("You've just made that up, baka!");
				return true;
			}
			if (!from.CanConvertTo(to))
			{
				await message.Channel.SendMessageAsync("Do you even understand basic maths? This isn't convertible!");
				return true;
			}
			var fromMeasurement = new Measurement((Ratio)value, from);
			await message.Channel.SendMessageAsync($"{fromMeasurement} = {fromMeasurement.ConvertTo(to)}");
			return true;
		}
	}
}
