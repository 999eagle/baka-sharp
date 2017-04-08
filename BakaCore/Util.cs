using System;
using System.Collections.Generic;
using System.Text;

namespace BakaCore
{
	static class Util
	{
		public static Func<TState, Exception, string> CreateLogMessageFormatter<TState>(Func<TState, string> stateFormatter)
		{
			return (state, excecption) =>
			{
				return FormatLogMessage(stateFormatter(state), excecption);
			};
		}
		public static string FormatLogMessage(string message, Exception exception)
		{
			var m = "";
			if (!String.IsNullOrWhiteSpace(message))
			{
				m = message;
			}
			if (exception != null)
			{
				if (!String.IsNullOrWhiteSpace(m))
				{
					m += "\n";
				}
				m += $"Exception {exception.GetType().Name} thrown in {exception.Source}:\n{exception.Message}";
			}
			return m;
		}
	}
}
