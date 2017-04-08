using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BakaCore
{
	public class Config
	{
		public ILoggerFactory LoggerFactory { get; set; } = null;
		public bool CommandsDisabled { get; set; } = false;
		public string CommandTag { get; set; } = "+";
	}
}
