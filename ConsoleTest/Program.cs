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
			var bakaConfig = config.Get<BakaCore.Configuration>();
			var bot = new BakaCore.BakaChan(bakaConfig);
			bakaConfig.Logging.LoggerFactory.AddConsole(bakaConfig.Logging.LogLevel);
			var runTask = bot.Run();
			while (Console.ReadLine() != "e") ;
			bot.Stop();
			await runTask;
		}
	}
}
