using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using BakaCore;

namespace BakaServiceLinux
{
	public class Service
	{
		ILoggerFactory loggerFactory;
		ILogger logger;
		BakaChan bakaChan;
		Task bakaRunTask;

		public int ExitCode { get; private set; }

		public Service()
		{
			ExitCode = 0;
			loggerFactory = new LoggerFactory();
			loggerFactory.AddNLog();
			logger = loggerFactory.CreateLogger<Service>();
		}

		public void Start()
		{
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
			logger.LogInformation("Service starting");
			logger.LogTrace("Loading configuration");
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddJsonFile("config.json");
			var config = configBuilder.Build();
			logger.LogTrace("Configuration loaded");
			var bakaConfig = config.Get<Configuration>();
			logger.LogTrace("Configuration bound");
			bakaConfig.Logging.LoggerFactory = loggerFactory;
			logger.LogInformation("Running Baka-chan");
			bakaChan = new BakaChan(bakaConfig);
			try
			{
				bakaRunTask = bakaChan.Run();
				logger.LogDebug("Baka-chan started");
			}
			catch (Exception ex)
			{
				logger.LogCritical(new EventId(), ex, "An unhandled exception was thrown during start-up");
				ExitCode = -1;
				Stop();
			}
		}

		public void Stop()
		{
			logger.LogTrace("Stopping baka-chan");
			bakaChan.Stop();
			try
			{
				if (bakaRunTask != null)
				{
					bakaRunTask.Wait();
				}
			}
			catch (Exception ex)
			{
				logger.LogError(new EventId(), ex, "Unhandled exception while stopping bot!");
			}
			logger.LogInformation("Service stopped");
		}
	}
}
