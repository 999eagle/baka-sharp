using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			Console.ReadLine();
		}

		static async Task AsyncMain(string[] args)
		{
			var generalConfig = config.GetSection("General");

			var bakaConfig = new BakaCore.Config();
			bakaConfig.CommandTag = generalConfig["CommandTag"];
			var bot = new BakaCore.BakaChan(bakaConfig);
			bakaConfig.LoggerFactory.AddConsole(generalConfig.GetValue<LogLevel>("LogLevel"));
			var runTask = bot.Run(config["Discord:LoginToken"]);
			while (Console.ReadLine() != "e") ;
			bot.Stop();
			await runTask;
		}
	}
}
