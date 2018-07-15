using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsoleTest
{
	class Program
	{
		private static IConfiguration config;
		static void Main(string[] args)
		{
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddJsonFile("config.json");
			config = configBuilder.Build();
			AsyncMain(args).Wait();
		}

		static async Task AsyncMain(string[] args)
		{
			var bakaConfig = config.Get<BakaCore.Configuration>();
			bakaConfig.Logging.LoggerFactory = new LoggerFactory();
			bakaConfig.Logging.LoggerFactory.AddConsole(bakaConfig.Logging.LogLevel);
			var logger = bakaConfig.Logging.LoggerFactory.CreateLogger("Baka-chan");
			logger.LogInformation("Initializing Baka-chan");
			var bot = new BakaCore.BakaChan(bakaConfig);
			Task runTask = null;

			Console.CancelKeyPress += (sender, evArgs) => { Exit(); };
			AppDomain.CurrentDomain.ProcessExit += (sender, evArgs) => { Exit(); };

			logger.LogDebug("Starting bot instance");
			try
			{
				runTask = bot.Run();
			}
			catch (Exception ex)
			{
				logger.LogCritical(ex, "Exception thrown during bot startup");
				Exit();
			}
			if (runTask != null)
			{
				await runTask;
			}

			void Exit()
			{
				logger.LogDebug("Stopping bot instance");
				bot.Stop();
				if (runTask != null)
				{
					try
					{
						runTask.Wait();
					}
					catch (Exception ex)
					{
						logger.LogCritical(ex, "Exception thrown during bot shutdown");
					}
				}
				logger.LogInformation("Bot instance stopped");
			}
		}
	}
}
