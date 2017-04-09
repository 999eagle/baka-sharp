using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord.WebSocket;

namespace BakaCore.Commands
{
	interface ICommandDescription
	{
		string[] Commands { get; }
		string Subcommand { get; }
		string Help { get; }
	}

	class CommandDescription : ICommandDescription
	{
		public string[] Commands { get; }
		public string Subcommand { get; set; }
		public string Help { get; set; }

		public string UsageString { get; set; }
		public Func<SocketMessage, string[], Task<bool>> Invoke { get; set; }

		public CommandDescription(params string[] commands)
		{
			Commands = commands;
		}

		public static CommandDescription CreateCommandDescription(ICommandDescription original)
		{
			if (original is CommandDescription descr) return descr;

			return new CommandDescription(original.Commands)
			{
				Help = original.Help,
				Subcommand = original.Subcommand
			};
		}

		public string GetFullUsage() => $"{Commands[0]}{(Subcommand == null ? "" : $" {Subcommand}")}{UsageString}";
	}

	[AttributeUsage(AttributeTargets.Method)]
	class CommandAttribute : Attribute, ICommandDescription
	{
		public string[] Commands { get; }
		public string Subcommand { get; set; }
		public string Help { get; set; }

		public CommandAttribute(params string[] commands)
		{
			Commands = commands;
		}
	}
}
