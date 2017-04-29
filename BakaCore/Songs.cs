using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Discord.WebSocket;

namespace BakaCore
{
	class Songs : IDisposable
	{
		private ILogger logger;
		private DiscordSocketClient client;

		private readonly string[][] lyrics = new[] {
			new[] { "What is love?", "Baby don't hurt me", "Don't hurt me", "No more" },
			new[] { "Never gonna give you up", "Never gonna let you down", "Never gonna run around and desert you", "Never gonna make you cry", "Never gonna say goodbye", "Never gonna tell a lie and hurt you" },
			new[] { "I'm blue", "Da ba dee da ba daa" },
			new[] { "And I know why", "Coz I got high" },
			new[] { "I wanna be the very best", "Like no one ever was", "To catch them is my real test", "To train them is my cause", "I will travel across the land", "Searching far and wide", "Each Pokémon to understand", "The power that's inside", "Pokémon, gotta catch 'em all!", "It's you and me", "I know it's my destiny", "Pokémon, oh, you're my best friend", "In a world we must defend", "Pokémon, gotta catch 'em all!", "A heart so true", "Our courage will pull us through", "You teach me and I'll teach you", "Pokémon, gotta catch 'em all!", "Gotta catch 'em all!", "Yeah" },
			new[] { "Po-a-mon", "(Tötet sie alle!)" },
			new[] { "It's beginning to look a lot like murder", "Everywhere you go", "Senpai's bothersome childhood friend", "Is flirting with him again", "She wants him, but I'll never let him go", "It's beginning to look a lot like murder", "Lots of blood and gore", "But the prettiest sight to see", "Is Senpai beneath a tree", "The boy I adore!", "A rusty pipe made of lead and a blow to the head", "Will take care of Kokona-chan!", "A shiny sharp axe and one glorious whack", "To eliminate Osana-chan!", "Oh golly, I can hardly wait for school to start again", "It's beginning to look a lot like murder", "Everywhere you go", "Well I could send her straight to hell", "Or just get her expelled", "Stab her quick or gut her nice and slow", "It's beginning to look a lot like murder", "Killing is an art!", "Won't you please stop your struggling", "You know I just want one thing", "To carve out your heart" },
		};
		private readonly string[][] normalizedLyrics;
		private IDictionary<ulong, IDictionary<uint, uint>> songStates;
		private IDictionary<(ulong channel, uint song), (CancellationTokenSource token, Task task)> cancellationTokens;

		public Songs(ILoggerFactory loggerFactory, DiscordSocketClient client)
		{
			this.client = client;
			this.logger = loggerFactory.CreateLogger<Songs>();
			this.client.MessageReceived += MessageReceived;

			normalizedLyrics = lyrics.Select(s => s.Select(NormalizeLyrics).ToArray()).ToArray();
			songStates = new Dictionary<ulong, IDictionary<uint, uint>>();
			cancellationTokens = new Dictionary<(ulong, uint), (CancellationTokenSource, Task)>();
			logger.LogInformation("Initialized");
		}

		public void Dispose()
		{
			foreach (var kv in cancellationTokens)
			{
				kv.Value.token.Cancel();
				kv.Value.task.GetAwaiter().GetResult();
			}
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
					SongTimeout(message.Channel.Id, songIdx);
				}
			}
		}

		private void SongTimeout(ulong channelId, uint songIdx)
		{
			if (cancellationTokens.TryGetValue((channelId, songIdx), out var tokenTask))
			{
				if (!tokenTask.task.IsCompleted)
				{
					tokenTask.token.Cancel();
					logger.LogDebug("Canceled previous song timeout.");
				}
				tokenTask.task.GetAwaiter().GetResult();
			}
			var tokenSource = new CancellationTokenSource();
			var task = Timeout();
			cancellationTokens[(channelId, songIdx)] = (tokenSource, task);
			logger.LogDebug($"Scheduled timeout for song {songIdx} in channel {channelId}.");

			async Task Timeout()
			{
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(30), tokenSource.Token);
					logger.LogDebug($"Song {songIdx} in channel {channelId} timed out.");
					songStates[channelId].Remove(songIdx);
					if (!songStates[channelId].Any())
					{
						songStates.Remove(channelId);
					}
					cancellationTokens.Remove((channelId, songIdx));
					logger.LogTrace("Cleaned up state.");
				}
				catch (TaskCanceledException) { }
				catch (Exception ex)
				{
					logger.LogError(new EventId(), ex, "Unhandled exception in song timeout!");
				}
			}
		}
	}
}
