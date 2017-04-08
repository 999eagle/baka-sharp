using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Discord.WebSocket;

namespace BakaCore.Commands
{
	class Command
	{
		public string HelpText { get; set; }
		public string[] Commands { get; set; }
		public string Subcommand { get; set; }
		public Func<SocketMessage, string[], Task<bool>> Invoke { get; set; }
		public string UsageString { get; set; }

		public string GetFullUsage() => $"{Commands[0]}{(Subcommand == null ? "" : $" {Subcommand}")}{UsageString}";
	}
}
