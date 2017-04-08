using System;
using System.Collections.Generic;
using System.Text;

using Discord.WebSocket;

namespace BakaCore.Data
{
	interface IDataStore
	{
		GuildData GetGuildData(SocketGuild guild);
	}
}
