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

namespace BakaCore.Commands
{
	class CommandHandler
	{
		private DiscordSocketClient client;
		private Configuration config;
		private ILogger logger;
		private IServiceProvider services;

		private List<CommandDescription> registeredCommands = new List<CommandDescription>();

		public CommandHandler(ILoggerFactory loggerFactory, DiscordSocketClient client, Configuration config, IServiceProvider services)
		{
			logger = loggerFactory.CreateLogger<CommandHandler>();
			this.client = client;
			this.config = config;
			this.client.MessageReceived += MessageReceived;
			this.services = services;
			RegisterCommands(this);
		}

		public void RegisterCommands<T>(T instance = null) where T : class
		{
			var classType = typeof(T).GetTypeInfo();
			if (instance == null)
			{
				foreach (var ctor in classType.GetConstructors())
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
			foreach (var meth in classType.GetMethods().Where(mi => mi.GetCustomAttribute<CommandAttribute>() != null))
			{
				var attr = meth.GetCustomAttribute<CommandAttribute>();
				registeredCommands.Add(CreateCommand(instance, meth, attr));
				logger.LogTrace($"Registered command method {meth.Name} in {classType.FullName} using reflection.");
			}
			var customCommandMethod = classType.GetMethod("GetCustomCommands");
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

		private async Task MessageReceived(SocketMessage message)
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
			foreach (var cmd in registeredCommands)
			{
				if (await cmd.Invoke(message, split))
				{
					break;
				}
			}
		}

		[Command("help", Help = "Shows this help")]
		public async Task HelpCommand(SocketMessage message)
		{
			var text = "**Baka-chan**\nMade by **The999eagle#6302**\n\n";
			foreach (var command in registeredCommands)
			{
				text += $"`{config.Commands.Tag}{command.GetFullUsage()}`: {command.Help}\n";
			}
			var channel = await message.Author.CreateDMChannelAsync();
			await channel.SendMessageAsync(text);
		}

		private CommandDescription CreateCommand(object instance, MethodInfo meth, ICommandDescription description)
		{
			var command = CommandDescription.CreateCommandDescription(description);
			var commandArgs = meth.GetParameters().Skip(1).ToList();
			
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
					case ParameterInfo val when (val.ParameterType == typeof(string) || val.ParameterType == typeof(int)):
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
				var args = new List<object> { message };
				var parseIdx = 1;
				if (!description.Commands.Contains(split[parseIdx++])) { return false; }
				if (description.Subcommand != null && split[parseIdx++] != description.Subcommand) { return false; }
				logger.LogTrace($"Method {meth.Name} in {meth.DeclaringType.FullName} matched command {split[1]}{(description.Subcommand != null ? $" {description.Subcommand}" : "")}.");
				var argsMatch = true;
				for (int i = 0; i < commandArgs.Count; i++)
				{
					var optional = commandArgs[i].GetCustomAttribute<OptionalAttribute>() != null;
					var parseText = (i + parseIdx >= split.Length) ? null : split[i + parseIdx];
					if (!optional && parseText == null)
					{
						argsMatch = false;
						break;
					}
					switch (commandArgs[i])
					{
						case ParameterInfo arg when (arg.ParameterType == typeof(SocketUser)):
							if (parseText == null)
							{
								args.Add(null);
							}
							else if (MentionUtils.TryParseUser(parseText, out var userId))
							{
								args.Add(client.GetUser(userId));
							}
							else
							{
								argsMatch = false;
								break;
							}
							break;
						case ParameterInfo arg when (arg.ParameterType == typeof(string)):
							args.Add(parseText);
							break;
						case ParameterInfo arg when (arg.ParameterType == typeof(int)):
							if (Int32.TryParse(parseText, out int val))
							{
								args.Add(val);
							}
							else
							{
								argsMatch = false;
								break;
							}
							break;
						default:
							logger.LogWarning($"Unknown parameter type {commandArgs[i].ParameterType.FullName} for parameter {commandArgs[i].Name} in command method {meth.Name} in {meth.DeclaringType.FullName} encountered while parsing command parameters.");
							break;
					}
				}
				if (argsMatch)
				{
					var task = meth.Invoke(instance, args.ToArray());
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
				var commandsWithSameName = registeredCommands.Where(c => c.Commands.Contains(split[1])).ToList();
				if (commandsWithSameName.Count > commandsWithSameName.IndexOf(command) + 1)
					return false;
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
