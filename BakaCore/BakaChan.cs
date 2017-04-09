﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;
using SteamWebAPI2.Interfaces;

namespace BakaCore
{
	public class BakaChan
	{
		private DiscordSocketClient client;
		private Configuration config;
		private CancellationTokenSource cancellationTokenSource;
		private IServiceProvider services;
		private ILogger logger;

		public BakaChan(Configuration config)
		{
			this.config = config;

			ConfigureServices();

			var loggerFactory = services.GetRequiredService<ILoggerFactory>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<BakaChan>();
		}

		private void ConfigureServices()
		{
			var services = new ServiceCollection();
			services
				.AddSingleton(config)
				.AddSingleton(new DiscordSocketConfig()
				{
					LogLevel = LogSeverity.Debug
				})
				.AddSingleton<DiscordSocketClient>()
				.AddSingleton<Commands.CommandHandler>()
				.AddSingleton<ISteamUser>(new SteamUser(config.API.SteamWebAPIKey))
				.AddSingleton<ISteamUserStats>(new SteamUserStats(config.API.SteamWebAPIKey))
				.AddSingleton<Data.IDataStore, Data.JsonStore>();
			if (config.Logging.LoggerFactory == null)
			{
				config.Logging.LoggerFactory = new LoggerFactory();
			}
			services.AddSingleton(config.Logging.LoggerFactory);

			this.services = services.BuildServiceProvider();
		}

		public async Task Run()
		{
			logger.LogInformation($"Starting bot.");
			Initialize();
			cancellationTokenSource = new CancellationTokenSource();
			await client.LoginAsync(TokenType.Bot, config.API.DiscordLoginToken);
			await client.StartAsync();
			
			var commandHandler = services.GetRequiredService<Commands.CommandHandler>();
			commandHandler.RegisterCommands<Commands.GeneralCommands>();
			commandHandler.RegisterCommands<Commands.SteamCommands>();
			commandHandler.RegisterCommands<Commands.CoinsCommands>();
			try
			{
				// Wait until the token is cancelled
				await Task.Delay(-1, cancellationTokenSource.Token);
			}
			catch (TaskCanceledException)
			{
				logger.LogInformation($"Stopping bot.");
			}
			await client.SetStatusAsync(UserStatus.Offline);
			await client.StopAsync();
			await client.LogoutAsync();
		}

		public void Stop()
		{
			logger.LogDebug($"Sending stop signal.");
			cancellationTokenSource.Cancel();
		}

		private void Initialize()
		{
			client = services.GetRequiredService<DiscordSocketClient>();
			client.Log += DispatchDiscordLogMessage;
			client.Ready += Ready;

			async Task Ready()
			{
				logger.LogInformation($"Discord client ready. UserID: {client.CurrentUser.Id}");
				await client.SetStatusAsync(UserStatus.Online);
				await client.SetGameAsync($"Use {config.Commands.Tag}help");
			}
			async Task DispatchDiscordLogMessage(LogMessage message)
			{
				await Task.Run(() =>
				{
					LogLevel level;
					switch (message.Severity)
					{
						case LogSeverity.Debug:
							level = LogLevel.Trace;
							break;
						case LogSeverity.Verbose:
							level = LogLevel.Debug;
							break;
						case LogSeverity.Info:
							level = LogLevel.Information;
							break;
						case LogSeverity.Warning:
							level = LogLevel.Warning;
							break;
						case LogSeverity.Critical:
							level = LogLevel.Critical;
							break;
						case LogSeverity.Error:
						default:
							level = LogLevel.Error;
							break;
					}
					var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(message.Source);
					logger.Log(level, new EventId(), message.Message, message.Exception, Util.FormatLogMessage);
				});
			}
		}
	}
}
