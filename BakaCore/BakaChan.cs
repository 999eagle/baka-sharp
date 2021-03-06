using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;
using SteamWebAPI2.Interfaces;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;

using BakaCore.Services;

namespace BakaCore
{
	public class BakaChan
	{
		private DiscordSocketClient client;
		private Configuration config;
		private CancellationTokenSource cancellationTokenSource;
		private IServiceProvider services;
		private IServiceScope instanceServiceScope;
		private ILogger logger;

		private IList<Type> miscHandlers = new List<Type>
		{
			typeof(MiscHandlers.Greeting),
			typeof(MiscHandlers.Songs),
			typeof(MiscHandlers.UnitConverter),
		};

		public BakaChan(Configuration config)
		{
			this.config = config;

			ConfigureServices();

			var loggerFactory = services.GetRequiredService<ILoggerFactory>();
			logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<BakaChan>();

			void ConfigureServices()
			{
				var services = new ServiceCollection();
				services
					.AddSingleton(config);

				if (config.Logging.LoggerFactory == null)
				{
					config.Logging.LoggerFactory = new LoggerFactory();
				}
				services.AddSingleton(config.Logging.LoggerFactory);

				services
					.AddScoped((_) => new DiscordSocketConfig()
					{
						LogLevel = LogSeverity.Debug,
						RestClientProvider = Discord.Net.Rest.DefaultRestClientProvider.Create(useProxy: true),
						WebSocketProvider = Discord.Net.WebSockets.DefaultWebSocketProvider.Create(WebRequest.DefaultWebProxy)
					})
					.AddScoped<DiscordSocketClient>()
					.AddScoped<Commands.CommandHandler>()
					.AddScoped<Commands.ArgumentParser>()
					.AddScoped<Data.IDataStore, Data.JsonStore>()
					.AddScoped<Data.LiteDBStore>()
					.AddScoped((_) => new Random())
					.AddScoped<ImageService>()
					.AddScoped((_) => new BaseClientService.Initializer()
					{
						ApiKey = config.API.GoogleAPIKey,
						ApplicationName = "Baka-chan"
					})
					.AddScoped<YouTubeService>()
					.AddScoped<MusicService>()
					.AddScoped<IMusicEncoderService, FFmpegEncoderService>()
					.AddScoped<BakaCore.Services.PlayerService>()
					.AddScoped<CoreEvents>();

				foreach (var handlerType in miscHandlers)
				{
					services.AddScoped(handlerType);
				}

				this.services = services.BuildServiceProvider();
			}
		}

		public Task Run()
		{
			logger.LogInformation($"Starting bot.");
			instanceServiceScope = services.CreateScope();
			Initialize();
			var events = instanceServiceScope.ServiceProvider.GetRequiredService<CoreEvents>();
			cancellationTokenSource = new CancellationTokenSource();
			client.LoginAsync(TokenType.Bot, config.API.DiscordLoginToken).Wait();
			client.StartAsync().Wait();

			logger.LogDebug("Registering commands.");
			var commandHandler = instanceServiceScope.ServiceProvider.GetRequiredService<Commands.CommandHandler>();
			commandHandler.RegisterCommands<Commands.GeneralCommands>();
			commandHandler.RegisterCommands<Commands.SteamCommands>();
			commandHandler.RegisterCommands<Commands.CoinsCommands>();
			commandHandler.RegisterCommands<Commands.GameCommands>();
			commandHandler.RegisterCommands<Commands.SettingsCommands>();
			commandHandler.RegisterCommands<Commands.UnitCommands>();
			commandHandler.RegisterCommands<Commands.MusicCommands>();
			logger.LogDebug("Initializing misc handlers.");
			var handlerInstances = miscHandlers.Select(type => instanceServiceScope.ServiceProvider.GetRequiredService(type)).ToList();
			return RunAsync();
			async Task RunAsync()
			{
				await events.RaiseInitializationDone();
				try
				{
					// Wait until the token is cancelled
					while (true)
					{
						await Task.Delay(TimeSpan.FromDays(1), cancellationTokenSource.Token);
						cancellationTokenSource.Token.ThrowIfCancellationRequested();
						logger.LogInformation("Running daily event");
						try
						{
							await events.RaiseDailyEvent();
							logger.LogDebug("Daily event ran");
						}
						catch (Exception ex)
						{
							logger.LogError(ex, "Exception while running daily event");
						}
					}
				}
				catch (TaskCanceledException)
				{
					logger.LogInformation($"Stopping bot.");
				}
				await events.RaiseBotShuttingDown();
				await client.SetStatusAsync(UserStatus.Offline);
				await client.StopAsync();
				await client.LogoutAsync();
				instanceServiceScope.Dispose();
				instanceServiceScope = null;
			}
		}

		public void Stop()
		{
			logger.LogDebug($"Sending stop signal.");
			cancellationTokenSource.Cancel();
		}

		private void Initialize()
		{
			logger.LogInformation("Setting library path");
			NativeLibraryHandler.SetLibraryPath();

			client = instanceServiceScope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
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
					var logger = instanceServiceScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(message.Source);
					logger.Log(level, new EventId(), message.Message, message.Exception, Util.FormatLogMessage);
				});
			}
		}
	}
}
