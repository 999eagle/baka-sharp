using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BakaCore;

namespace BakaChan
{
	public class Service
	{
		private ILogger logger;
		private BakaCore.BakaChan botInstance;
		private Task botRunTask;

		public void Start()
		{
			var config = InitConfig();
			var loggerFactory = new LoggerFactory();
			loggerFactory.AddConsole(config.Logging.LogLevel);
			logger = loggerFactory.CreateLogger<Service>();
			config.Logging.LoggerFactory = loggerFactory;
			logger.LogInformation("Baka-chan starting");

			try
			{
				logger.LogDebug("Initializing bot instance");
				botInstance = new BakaCore.BakaChan(config);
				logger.LogDebug("Running bot instance");
				botRunTask = botInstance.Run();
			}
			catch (Exception ex)
			{
				logger.LogCritical(ex, "An unhandled exception was thrown during start-up");
				throw;
			}
		}

		public void Stop()
		{
			if (botInstance == null) return;
			logger?.LogDebug("Stopping bot instance");
			botInstance.Stop();
			if (botRunTask != null)
			{
				try
				{
					botRunTask.GetAwaiter().GetResult();
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "An unhandled exception was thrown during shutdown");
				}
			}
			logger?.LogInformation("Bot instance stopped");
		}

		private Configuration InitConfig()
		{
			try
			{
				var configBuilder = new ConfigurationBuilder();
				configBuilder.AddJsonFile("config.json");
				var config = configBuilder.Build();
				return config.Get<Configuration>();
			}
			catch (Exception ex)
			{
				// create temporary logger just for logging this single exception as we don't have a configuration yet for the logger
				var logger = new LoggerFactory().AddConsole().CreateLogger($"{nameof(Service)}.{nameof(InitConfig)}");
				logger.LogCritical(ex, "Failed to load configuration");
				throw;
			}
		}
	}
}
