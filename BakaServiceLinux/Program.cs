using System;
using System.Threading;

namespace BakaServiceLinux
{
	class Program
	{
		public static int Main(string[] args)
		{
			var service = new Service();
			service.Start();
			if (service.ExitCode != 0)
			{
				return service.ExitCode;
			}
			var waitEvent = new ManualResetEvent(false);
			AppDomain.CurrentDomain.ProcessExit += OnExit;
			waitEvent.WaitOne();
			return service.ExitCode;

			void OnExit(object sender, EventArgs eventArgs)
			{
				service.Stop();
				waitEvent.Set();
			}
		}
	}
}
