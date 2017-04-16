using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Discord.WebSocket;

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

		public static async Task<SocketMessage> WaitForMessage(this SocketChannel channel, TimeSpan timeout, Func<SocketMessage, bool> filter)
		{
			var cancellationTokenSource = new CancellationTokenSource();
			SocketMessage matchingMessage = null;
			channel.Discord.MessageReceived += MessageReceived;
			try
			{
				await Task.Delay(timeout, cancellationTokenSource.Token);
			}
			catch (TaskCanceledException) { }
			channel.Discord.MessageReceived -= MessageReceived;
			return matchingMessage;

			Task MessageReceived(SocketMessage message)
			{
				return Task.Run(() =>
				{
					if (filter(message))
					{
						matchingMessage = message;
						cancellationTokenSource.Cancel();
					}
				});
			}
		}
	}
}
