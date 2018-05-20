using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BakaCore
{
	public delegate Task AsyncEventHandler();
	public delegate Task AsyncEventHandler<in TArgs>(object sender, TArgs eventArgs);

	public static class EventExtensions
	{
		public static AsyncEventHandler<EventArgs> Async(this EventHandler eventHandler)
		{
			return (sender, eventArgs) =>
			{
				eventHandler(sender, eventArgs);
				return Task.CompletedTask;
			};
		}

		public static AsyncEventHandler<TArgs> Async<TArgs>(this EventHandler<TArgs> eventHandler)
		{
			return (sender, eventArgs) =>
			{
				eventHandler(sender, eventArgs);
				return Task.CompletedTask;
			};
		}

		public static Task InvokeAsync(this AsyncEventHandler eventHandler)
		{
			var handler = eventHandler;
			if (handler == null) return Task.CompletedTask;
			return Task.WhenAll(handler.GetInvocationList().Cast<AsyncEventHandler>().Select(d => d.Invoke()));
		}

		public static Task InvokeAsync<TArgs>(this AsyncEventHandler<TArgs> eventHandler, object sender, TArgs eventArgs)
		{
			var handler = eventHandler;
			if (handler == null) return Task.CompletedTask;
			return Task.WhenAll(handler.GetInvocationList().Cast<AsyncEventHandler<TArgs>>().Select(d => d.Invoke(sender, eventArgs)));
		}
	}

	public class CoreEvents
	{
		public event AsyncEventHandler BotShuttingDown;
		public Task RaiseBotShuttingDown() => BotShuttingDown.InvokeAsync();
		public event AsyncEventHandler InitializationDone;
		public Task RaiseInitializationDone() => InitializationDone.InvokeAsync();
	}
}
