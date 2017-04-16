using System;
using System.Collections.Generic;
using System.Text;

using Discord.WebSocket;

namespace BakaCore.Data
{
	[Flags]
	enum Permissions : uint
	{
		None = 0,
		SpawnCoins = (1u << 0),
		DespawnCoins = (1u << 1),
		Strike = (1u << 2),
		EditPermissions = (1u << 3),
		DisplayPermissions = (1u << 4),
		Settings = (1u << 5),
		Ban = (1u << 6),
		Kick = (1u << 7),
		Mute = (1u << 8),
		Purge = (1u << 9),
	}

	static class PermissionExtensions
	{
		public static bool RoleHasPermission(this GuildData data, SocketRole role, Permissions permission)
		{
			if (permission == Permissions.None) return true;
			if (role.Permissions.Administrator) return true;
			return (data.GetPermissions(role) & permission) == permission;
		}

		public static bool UserHasPermission(this GuildData data, SocketUser user, Permissions permission)
		{
			if (permission == Permissions.None) return true;
			if (user is SocketGuildUser guildUser)
			{
				foreach (var role in guildUser.Roles)
				{
					if (data.RoleHasPermission(role, permission))
						return true;
				}
			}
			return (data.GetPermissions(user) & permission) == permission;
		}

		public static void AddPermission(this GuildData data, SocketEntity<ulong> entity, Permissions permission)
		{
			data.SetPermissions(entity, data.GetPermissions(entity) | permission);
		}

		public static void RemovePermission(this GuildData data, SocketEntity<ulong> entity, Permissions permission)
		{
			data.SetPermissions(entity, data.GetPermissions(entity) & (~permission));
		}
	}
}
