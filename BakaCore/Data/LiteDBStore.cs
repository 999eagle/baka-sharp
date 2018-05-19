using System;
using System.IO;
using LiteDB;

namespace BakaCore.Data
{
	public class LiteDBStore : IDisposable
	{
		private readonly LiteDatabase db = null;
		public SongCollection SongCollection { get; private set; }

		public LiteDBStore(Configuration config)
		{
			db = new LiteDatabase(Path.Combine(config.DataStore.DataPath, "litedb.db"));
			SongCollection = new SongCollection(db);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				db?.Dispose();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
