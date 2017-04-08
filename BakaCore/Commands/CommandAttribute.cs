using System;
using System.Collections.Generic;
using System.Text;

namespace BakaCore.Commands
{
	[AttributeUsage(AttributeTargets.Method)]
	class CommandAttribute : Attribute
	{
		public string[] Commands { get; }
		public string Subcommand { get; set; }
		public string Help { get; set; }

		public CommandAttribute(params string[] commands)
		{
			Commands = commands;
		}
	}

	[AttributeUsage(AttributeTargets.Parameter)]
	class OptionalAttribute : Attribute
	{
	}
}
