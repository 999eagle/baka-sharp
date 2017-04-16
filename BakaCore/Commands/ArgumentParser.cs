using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

using Discord;
using Discord.WebSocket;

namespace BakaCore.Commands
{
	class ArgumentParser
	{
		private ILogger logger;
		private DiscordSocketClient client;

		private IDictionary<Type, Func<string, (bool success, object result)>> simpleParseTypes = new Dictionary<Type, Func<string, (bool success, object result)>>();

		public ArgumentParser(ILoggerFactory loggerFactory, DiscordSocketClient client)
		{
			logger = loggerFactory.CreateLogger<ArgumentParser>();
			this.client = client;
			simpleParseTypes.Add(typeof(string), ParseSimpleString);
			simpleParseTypes.Add(typeof(int), ParseSimpleInt);
			simpleParseTypes.Add(typeof(SocketUser), ParseSimpleSocketUser);
			simpleParseTypes.Add(typeof(SocketRole), ParseSimpleSocketRole);
			simpleParseTypes.Add(typeof(IMentionable), ParseSimpleSocketRoleOrUser);
		}

		(bool, object) ParseSimpleString(string input)
		{
			return (true, input);
		}
		(bool, object) ParseSimpleInt(string input)
		{
			if (input != null && Int32.TryParse(input, out int val))
				return (true, val);
			else
				return (false, null);
		}
		(bool, object) ParseSimpleSocketUser(string input)
		{
			if (input != null && MentionUtils.TryParseUser(input, out var userId))
				return (true, client.GetUser(userId));
			else
				return (false, null);
		}
		(bool, object) ParseSimpleSocketRole(string input)
		{
			if (input != null && MentionUtils.TryParseRole(input, out var roleId))
				return (true, client.GetRole(roleId));
			else
				return (false, null);
		}
		(bool, object) ParseSimpleSocketRoleOrUser(string input)
		{
			var t = ParseSimpleSocketUser(input);
			if (t.Item1)
				return t;
			else
				return ParseSimpleSocketRole(input);

		}
		public bool TryParseArguments(IEnumerable<ParameterInfo> parameters, IEnumerable<string> inputs, out IEnumerable<object> parsed)
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
				case ParameterInfo arg when (arg.ParameterType.IsArray && arg.ParameterType.GetElementType() is Type elementType && simpleParseTypes.ContainsKey(elementType)):
					var separatorAttr = arg.GetCustomAttribute<ListSeparatorAttribute>();
					IEnumerable<string> items;
					if (separatorAttr != null)
					{
						items = String.Join(" ", inputs).Split(new[] { separatorAttr.Separator }, StringSplitOptions.None).Select(s => s.Trim());
					}
					else
					{
						items = inputs;
					}
					var objects = new List<object>();
					(bool success, object result) tuple;
					while (items.Any())
					{
						tuple = simpleParseTypes[elementType](items.FirstOrDefault());
						items = items.Skip(1);
						if (tuple.success)
						{
							objects.Add(tuple.result);
						}
						else
						{
							objects.Clear();
							break;
						}
					}
					if (objects.Any())
					{
						parsedInputCount = inputs.Count();
						var array = Array.CreateInstance(elementType, objects.Count);
						for (int i = 0; i < array.Length; i++)
						{
							array.SetValue(objects[i], i);
						}
						parsedObject = array;
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
