using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;
using BakaCore.Data;

namespace BakaCore.Commands
{
	class CommandHandler
	{
		private DiscordSocketClient client;
		private Configuration config;
		private ILogger logger;
		private IServiceProvider services;
		private ArgumentParser parser;
		private IDataStore dataStore;

		private List<CommandDescription> registeredCommands = new List<CommandDescription>();

		public CommandHandler(ILoggerFactory loggerFactory, DiscordSocketClient client, Configuration config, IServiceProvider services, ArgumentParser parser, IDataStore dataStore)
		{
			logger = loggerFactory.CreateLogger<CommandHandler>();
			this.client = client;
			this.config = config;
			this.client.MessageReceived += MessageReceived;
			this.services = services;
			this.parser = parser;
			this.dataStore = dataStore;
			RegisterCommands(this);
		}

		public void RegisterCommands<T>(T instance = null) where T : class
		{
			var classType = typeof(T).GetTypeInfo();
			if (instance == null)
			{
				foreach (var ctor in classType.DeclaredConstructors)
				{
					var parameters = ctor.GetParameters();
					if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceProvider))
					{
						instance = (T)ctor.Invoke(new[] { services });
						logger.LogTrace($"Created instance of {classType.FullName} using IServiceProvider.");
						break;
					}
					if (parameters.Length == 0)
					{
						instance = (T)ctor.Invoke(null);
						logger.LogTrace($"Created instance of {classType.FullName} using parameterless constructor.");
						break;
					}
				}
				if (instance == null)
				{
					logger.LogWarning($"Couldn't create instance of {classType.FullName}. No commands registered.");
					return;
				}
			}
			foreach (var meth in classType.DeclaredMethods.Where(mi => mi.GetCustomAttribute<CommandAttribute>() != null))
			{
				var attr = meth.GetCustomAttribute<CommandAttribute>();
				registeredCommands.Add(CreateCommand(instance, meth, attr));
				logger.LogTrace($"Registered command method {meth.Name} in {classType.FullName} using reflection.");
			}
			var customCommandMethod = classType.GetDeclaredMethod("GetCustomCommands");
			if (customCommandMethod != null)
			{
				logger.LogTrace($"Found custom commands in {classType.FullName}.");
				var customCommands = ((MethodInfo meth, ICommandDescription description)[])customCommandMethod.Invoke(instance, null);
				foreach (var command in customCommands)
				{
					registeredCommands.Add(CreateCommand(instance, command.meth, command.description));
					logger.LogTrace($"Registered custom command method {command.meth.Name} in {classType.FullName}.");
				}
			}
			logger.LogInformation($"Registered commands in Type {classType.FullName}");
		}

		private Task MessageReceived(SocketMessage message)
		{
			var backgroundStuff = Task.Run(() =>
			{
				if (message.Content.Length == 0) return;
				logger.LogTrace($"Message received: {message.Content}");
				if (config.Commands.Disabled) return;
				// split message and normalize array for the tag
				var command = message.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
				if (!config.Commands.Tag.EndsWith(" ") && command[0].StartsWith(config.Commands.Tag))
				{
					if (command[0].Length == config.Commands.Tag.Length)
					{
						command.RemoveAt(0);
					}
					else
					{
						command[0] = command[0].Substring(config.Commands.Tag.Length);
					}
					command.Insert(0, config.Commands.Tag);
				}
				var split = command.ToArray();
				if (split[0] == config.Commands.Tag)
				{
					bool success = false;
					foreach (var cmd in registeredCommands)
					{
						success = cmd.Invoke(message, split).GetAwaiter().GetResult();
						if (success)
						{
							break;
						}
					}
					if (!success)
					{
						message.Channel.SendMessageAsync($"Use {config.Commands.Tag}help to get a list of available commands.").GetAwaiter().GetResult();
					}
				}
			});
			backgroundStuff.ContinueWith((task) =>
			{
				if (task.Exception != null)
				{
					logger.LogError(new EventId(), task.Exception, "Unhandled exception throw in MessageReceived handler!");
				}
			});
			return Task.CompletedTask;
		}

		[Command("help", Help = "Shows this help")]
		public async Task HelpCommand(SocketMessage message)
		{
			var text = "**Baka-chan**\nMade by **The999eagle#6302**\n\n";
			foreach (var command in registeredCommands)
			{
				if (command.Help == null) continue;
				text += $"`{config.Commands.Tag}{command.GetFullUsage()}`";
				if (command.Help != "") text += $": {command.Help}";
				text += "\n";
			}
			await message.Author.SendMessageAsync(text);
		}

		private CommandDescription CreateCommand(object instance, MethodInfo meth, ICommandDescription description)
		{
			var command = CommandDescription.CreateCommandDescription(description);
			var commandArgs = meth.GetParameters().Skip(1);
			
			command.UsageString = "";
			foreach (var arg in commandArgs)
			{
				string usage = "";
				switch (arg)
				{
					case ParameterInfo customUsageParam when (customUsageParam.GetCustomAttribute<CustomUsageTextAttribute>() is CustomUsageTextAttribute attribute):
						usage = attribute.Usage;
						break;
					case ParameterInfo user when (user.ParameterType == typeof(SocketUser)):
						usage = "<@user>";
						break;
					case ParameterInfo role when (role.ParameterType == typeof(SocketRole)):
						usage = "<@role>";
						break;
					case ParameterInfo mention when (mention.ParameterType == typeof(IMentionable)):
						usage = "(<@user>|<@role>)";
						break;
					case ParameterInfo val when (val.ParameterType == typeof(string) || val.ParameterType == typeof(int) || val.ParameterType == typeof(double)):
						usage = $"<{val.Name}>";
						break;
					default:
						logger.LogWarning($"Unknown parameter type {arg.ParameterType.FullName} for parameter {arg.Name} in command method {meth.Name} in {meth.DeclaringType.FullName} encountered while generating usage text.");
						break;
				}
				if (arg.GetCustomAttribute<OptionalAttribute>() != null)
				{
					usage = $"[{usage}]";
				}
				command.UsageString += " " + usage;
			}

			command.Invoke = async (message, split) =>
			{
				var parseIdx = 1;
				if (split.Length <= parseIdx || !description.Commands.Contains(split[parseIdx++])) { return false; }
				if (description.Subcommand != null && (split.Length <= parseIdx || split[parseIdx++] != description.Subcommand)) { return false; }
				logger.LogTrace($"Method {meth.Name} in {meth.DeclaringType.FullName} matched command {split[1]}{(description.Subcommand != null ? $" {description.Subcommand}" : "")}.");
				if (!description.Scope.HasFlag(CommandScope.DM) && message.Channel is SocketDMChannel)
				{
					await message.Channel.SendMessageAsync("This command may not be used in a private chat.");
					return true;
				}
				if (!description.Scope.HasFlag(CommandScope.Group) && message.Channel is SocketGroupChannel)
				{
					await message.Channel.SendMessageAsync("This command may not be used in a group chat.");
					return true;
				}
				if (!description.Scope.HasFlag(CommandScope.Guild) && message.Channel is SocketGuildChannel)
				{
					await message.Channel.SendMessageAsync("This command may not be used in a guild chat.");
					return true;
				}
				if (description.RequiredPermissions != Permissions.None && message.Channel is SocketGuildChannel guildChannel && !dataStore.GetGuildData(guildChannel.Guild).UserHasPermission(message.Author, description.RequiredPermissions))
				{
					await message.Channel.SendMessageAsync("You don't have the necessary permissions for this command.");
					return true;
				}
				if (parser.TryParseArguments(commandArgs, split.Skip(parseIdx), out var parsed))
				{
					var task = meth.Invoke(instance, new object[] { message }.Concat(parsed).ToArray());
					if (task is Task<bool> boolTask)
					{
						if (await boolTask)
						{
							return true;
						}
					}
					else if (task is Task t)
					{
						await t;
						return true;
					}
				}
				else
				{
					logger.LogTrace($"Failed to match arguments.");
				}
				var commandsWithSameName = registeredCommands.Where(c => c.Commands.Contains(split[1]));
				if (commandsWithSameName.Count() > 1 && split.Length >= 3)
				{
					// subcommands exist and something is specified after command (either arg or subcommand)
					var sub = commandsWithSameName.FirstOrDefault(c => c.Subcommand == split[2]);
					if (sub != null && sub != command)
						// there's a valid subcommand specified, but we're not in it right now
						return false;
					if (sub == command)
						commandsWithSameName = new[] { command };
				}
				commandsWithSameName = commandsWithSameName.Where(c => c.Help != null);
				if (!commandsWithSameName.Any())
				{
					return false;
				}
				var text = "Usage:";
				foreach (var c in commandsWithSameName)
				{
					text += $"\n`{config.Commands.Tag}{c.GetFullUsage()}`";
				}
				await message.Channel.SendMessageAsync(text);
				return true;
			};
			return command;
		}
	}
}
