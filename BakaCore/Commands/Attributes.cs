using System;
using System.Collections.Generic;
using System.Text;

namespace BakaCore.Commands
{
	[AttributeUsage(AttributeTargets.Parameter)]
	class OptionalAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Parameter)]
	class CustomUsageTextAttribute : Attribute
	{
		public string Usage { get; }

		public CustomUsageTextAttribute(string usage)
		{
			Usage = usage;
		}
	}
}
