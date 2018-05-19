using System;
using LiteDB;

using BakaCore.Music;

namespace BakaCore.Data.Models
{
	public class SongData
	{
		[BsonId]
		public string Id { get; set; }
		public SongMetadata Metadata { get; set; }
		public string FileId { get; set; }
	}
}
