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

		private IDictionary<Type, Func<string, (bool success, object result)>> simpleParseTypes = new Dictionary<Type, Func<string, (bool success, object result)>>();

		(bool, object) ParseSimpleString(string input)
		{
			return (true, input);
		}
		(bool, object) ParseSimpleInt(string input)
		{
			if (Int32.TryParse(input, out int val))
				return (true, val);
			else
				return (false, null);
		}
		(bool, object) ParseSimpleSocketUser(string input)
		{
			if (MentionUtils.TryParseUser(input, out var userId))
				return (true, client.GetUser(userId));
			else
				return (false, null);
		}

		public CommandHandler(ILoggerFactory loggerFactory, DiscordSocketClient client, Configuration config, IServiceProvider services)
		{
			logger = loggerFactory.CreateLogger<CommandHandler>();
			this.client = client;
			this.config = config;
			this.client.MessageReceived += MessageReceived;
			this.services = services;
			RegisterCommands(this);
			simpleParseTypes.Add(typeof(string), ParseSimpleString);
			simpleParseTypes.Add(typeof(int), ParseSimpleInt);
			simpleParseTypes.Add(typeof(SocketUser), ParseSimpleSocketUser);
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
				var parseIdx = 1;
				if (!description.Commands.Contains(split[parseIdx++])) { return false; }
				if (description.Subcommand != null && split[parseIdx++] != description.Subcommand) { return false; }
				logger.LogTrace($"Method {meth.Name} in {meth.DeclaringType.FullName} matched command {split[1]}{(description.Subcommand != null ? $" {description.Subcommand}" : "")}.");
				if (TryParseArguments(commandArgs, split.Skip(parseIdx), out var parsed))
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

		bool TryParseArguments(IEnumerable<ParameterInfo> parameters, IEnumerable<string> inputs, out IEnumerable<object> parsed)
		{
			parsed = null;
			var info = parameters.FirstOrDefault();
			var input = inputs.FirstOrDefault();
			if (info == null)
			{
				if (input == null)
				{
					// reached the end of the list
					parsed = new object[0];
					return true;
				}
				else
				{
					// more input than parameters
					return false;
				}
			}
			var optional = info.GetCustomAttribute<OptionalAttribute>() != null;
			object parsedObject = null;
			int parsedInputCount = 0;
			switch (info)
			{
				case ParameterInfo arg when (simpleParseTypes.ContainsKey(arg.ParameterType)):
					(bool success, object parseResult) = simpleParseTypes[arg.ParameterType](input);
					if (success)
					{
						parsedInputCount = 1;
						parsedObject = parseResult;
					}
					break;
				default:
					logger.LogWarning($"Unknown parameter type {info.ParameterType.FullName} for parameter {info.Name} in command method {info.Member.Name} in {info.Member.DeclaringType.FullName} encountered while parsing command parameters.");
					break;
			}
			if (parsedInputCount == 0 && !optional)
			{
				// couldn't parse, but argument isn't optional
				return false;
			}
			if (TryParseArguments(parameters.Skip(1), inputs.Skip(parsedInputCount), out var subParsed))
			{
				parsed = new object[] { parsedObject }.Concat(subParsed);
				return true;
			}
			else
			{
				if (optional && TryParseArguments(parameters.Skip(1), inputs, out subParsed))
				{
					parsed = new object[] { parsedObject }.Concat(subParsed);
					return true;
				}
			}
			return false;
		}
	}
}
