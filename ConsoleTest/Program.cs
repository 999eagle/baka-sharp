using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace ConsoleTest
{
	class Program
	{
		static void Main(string[] args)
		{
			AsyncMain(args).Wait();
			Console.ReadLine();
		}

		static async Task AsyncMain(string[] args)
		{
			var config = new BakaCore.Config();
			var bot = new BakaCore.BakaChan(config);
			config.LoggerFactory.AddConsole(LogLevel.Debug);
			//Console.CancelKeyPress += (sender, e) => { bot.Stop(); };
			var runTask = bot.Run("<token>");
			while (Console.ReadLine() != "e") ;
			bot.Stop();
			await runTask;
		}
	}
}
