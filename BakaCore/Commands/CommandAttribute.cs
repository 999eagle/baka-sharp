using System;
using System.Collections.Generic;
using System.Text;

namespace BakaCore.Commands
{
	class CommandAttribute : Attribute
	{
		public string[] Commands { get; }
		public string Help { get; set; }
		public string Usage { get; set; }

		public CommandAttribute(string[] commands)
		{
			Commands = commands;
		}

		public CommandAttribute(string command)
			: this(new[] { command })
		{
		}
	}

	class OptionalAttribute : Attribute
	{
	}
}
