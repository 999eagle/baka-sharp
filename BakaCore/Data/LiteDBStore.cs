using System;
using System.IO;
using Microsoft.Extensions.Logging;
using LiteDB;

namespace BakaCore.Data
{
	public class LiteDBStore : IDisposable
	{
		private readonly LiteDatabase db = null;
		private ILogger logger;
		public SongCollection SongCollection { get; private set; }

		public LiteDBStore(ILoggerFactory loggerFactory, Configuration config, IServiceProvider services)
		{
			logger = loggerFactory.CreateLogger<LiteDBStore>();
			Directory.CreateDirectory(config.DataStore.DataPath);
			db = new LiteDatabase(Path.Combine(config.DataStore.DataPath, "litedb.db"));
			SongCollection = new SongCollection(db, loggerFactory, config, services);
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
