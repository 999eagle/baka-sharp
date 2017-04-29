using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;

namespace BakaCore
{
	class Songs
	{
		private ILogger logger;
		private DiscordSocketClient client;
		private readonly string[][] lyrics = new[] {
			new[] { "What is love?", "Baby don't hurt me", "Don't hurt me", "No more" },
			new[] { "Never gonna give you up", "Never gonna let you down", "Never gonna run around and desert you", "Never gonna make you cry", "Never gonna say goodbye", "Never gonna tell a lie and hurt you" },
		};
		private readonly string[][] normalizedLyrics;
		private IDictionary<ulong, IDictionary<uint, uint>> songStates;

		public Songs(ILoggerFactory loggerFactory, DiscordSocketClient client)
		{
			this.client = client;
			this.logger = loggerFactory.CreateLogger<Songs>();
			this.client.MessageReceived += MessageReceived;

			normalizedLyrics = lyrics.Select(s => s.Select(NormalizeLyrics).ToArray()).ToArray();
			songStates = new Dictionary<ulong, IDictionary<uint, uint>>();
			logger.LogInformation("Initialized");
		}

		private readonly char[] removeChars = new[] { '.', ',', '\'', '?', '!' };
		private string NormalizeLyrics(string lyrics) => new string(lyrics.Where(c => !removeChars.Contains(c)).ToArray()).ToLowerInvariant();

		private async Task MessageReceived(SocketMessage message)
		{
			string normalizedContent = NormalizeLyrics(message.Content);
			if (!songStates.TryGetValue(message.Channel.Id, out var channelState))
			{
				channelState = new Dictionary<uint, uint>();
				songStates.Add(message.Channel.Id, channelState);
			}
			for (uint songIdx = 0; songIdx < lyrics.Length; songIdx++)
			{
				if (!channelState.TryGetValue(songIdx, out var lineIdx))
				{
					lineIdx = 0;
				}
				if (lineIdx >= lyrics[songIdx].Length - 1) continue;
				if (normalizedContent.EndsWith(normalizedLyrics[songIdx][lineIdx]))
				{
					await message.Channel.SendMessageAsync(lyrics[songIdx][lineIdx + 1] + " :notes:");
					channelState[songIdx] = lineIdx + 2;
				}
			}
		}
	}
}
