using System;
using System.Collections.Generic;
using System.Text;

namespace BakaCore.Commands
{
	[AttributeUsage(AttributeTargets.Parameter)]
	class OptionalAttribute : Attribute { }
}
