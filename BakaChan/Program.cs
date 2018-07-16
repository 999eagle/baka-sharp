using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BakaChan
{
	class Program
	{
		static void Main(string[] args)
		{
			AsyncMain(args).Wait();
		}

		static async Task AsyncMain(string[] args)
		{
			var service = new Service();
			await RunAsync(service);
		}

		static async Task RunAsync(Service service)
		{
			var waitEvent = new ManualResetEventSlim(false);
			using (var tokenSource = new CancellationTokenSource())
			{
				AttachShutdownEvents(tokenSource, waitEvent);

				try
				{
					service.Start();

					var waitForCancel = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
					tokenSource.Token.Register((state) =>
					{
						((TaskCompletionSource<object>)state).TrySetResult(null);
					}, waitForCancel);
					await waitForCancel.Task;

					service.Stop();
				}
				finally
				{
					waitEvent.Set();
				}
			}
		}

		static void AttachShutdownEvents(CancellationTokenSource tokenSource, ManualResetEventSlim waitEvent)
		{
			void Shutdown()
			{
				if (!tokenSource.IsCancellationRequested)
				{
					try
					{
						tokenSource.Cancel();
					}
					catch (ObjectDisposedException) { }
				}
				waitEvent.Wait();
			}

			AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => Shutdown();
			Console.CancelKeyPress += (sender, eventArgs) =>
			{
				Shutdown();
				eventArgs.Cancel = true;
			};
		}
	}
}
