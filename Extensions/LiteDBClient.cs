using LiteDB;

using Microsoft.Extensions.Configuration;

using Serilog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace Qrame.Web.FileServer.Extensions
{
    public class LiteDBClient : ILiteDBClient
	{
		private string connectionString = "";

		private ILogger logger { get; }

		private IConfiguration configuration { get; }

		public LiteDBClient(ILogger logger, IConfiguration configuration)
		{
			this.logger = logger;
			this.configuration = configuration;
			var appSettings = configuration.GetSection("AppSettings");

			connectionString = $"Filename={Path.Combine(StaticConfig.ContentRootPath, "fileserver.db;")}";
			string liteDBOptions = appSettings["LiteDBOptions"].ToString();
			if (string.IsNullOrEmpty(liteDBOptions) == false)
			{
				liteDBOptions = liteDBOptions.ToString().Trim();
				connectionString = connectionString + (liteDBOptions.IndexOf(';') == 0 ? liteDBOptions.Substring(1, liteDBOptions.Length - 1) : liteDBOptions);
			}
		}

		public bool Insert<T>(T entity)
		{
			bool result = true;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					collection.Insert(entity);
				}
			}
			catch (Exception exception)
			{
				result = false;
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Insert");
			}

			return result;
		}

		public int Inserts<T>(List<T> entity)
		{
			int result = 0;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					result = collection.InsertBulk(entity);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Inserts");
			}

			return result;
		}

		public bool Update<T>(T entity)
		{
			bool result = true;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					collection.Update(entity);
				}
			}
			catch (Exception exception)
			{
				result = false;
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Update");
			}

			return result;
		}

		public int Updates<T>(List<T> entity)
		{
			int result = 0;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					result = collection.Update(entity);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Updates");
			}

			return result;
		}

		public bool Upsert<T>(T entity)
		{
			bool result = true;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					collection.Upsert(entity);
				}
			}
			catch (Exception exception)
			{
				result = false;
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Upsert");
			}

			return result;
		}

		public int Upserts<T>(List<T> entity)
		{
			int result = 0;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());
					result = collection.Upsert(entity);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Upserts");
			}

			return result;
		}

		public List<T> Select<T>(Expression<Func<T, bool>> filter, Expression<Func<T, string>> ensureIndex = null, int skip = 0, int limit = int.MaxValue)
		{
			List<T> result = null;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());

					if (ensureIndex != null)
					{
						collection.EnsureIndex(ensureIndex);
					}

					if (collection.Exists(filter) == true)
					{
						result = collection.Find(filter, skip, limit).ToList();
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Select");
			}

			return result;
		}

		public List<BsonDocument> PagedSelect(string collectionName, int pageNumber = 0, int entriesPerPage = int.MaxValue)
		{
			using (var db = new LiteDatabase(connectionString))
			{
				var col = db.GetCollection(collectionName);
				var query = col.Query();
				return query
					.Limit(entriesPerPage)
					.Offset((pageNumber - 1) * entriesPerPage)
					.ToList();
			}
		}

		public int Delete<T>(Expression<Func<T, bool>> filter = null)
		{
			int result = 0;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());

					if (filter != null)
					{
						collection.DeleteMany(filter);
					}
					else
					{
						collection.DeleteAll();
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Delete");
			}

			return result;
		}

		public bool SetFileStorage(string key, string filePath, Stream stream = null)
		{
			bool result = true;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					if (stream == null)
					{
						db.FileStorage.Upload(key, filePath);
					}
					else
					{
						db.FileStorage.Upload(key, filePath, stream);
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/SetFileStorage");
			}
			return result;
		}

		public LiteFileInfo<string> GetFileStorage(string key, Stream stream)
		{
			LiteFileInfo<string> result = null;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					result = db.FileStorage.Download(key, stream);
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/GetFileStorage");
			}
			return result;
		}

		public void ExportAll()
		{
			using (var db = new LiteDatabase(connectionString))
			{
				foreach (string collectionName in db.GetCollectionNames())
				{
					try
					{
						IEnumerable<BsonDocument> backupCollection = db.GetCollection(collectionName).FindAll();
						if (backupCollection != null)
						{
							string exportFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.collection");
							File.AppendAllText(exportFileName, JsonSerializer.Serialize(new BsonValue(backupCollection)));
						}
					}
					catch (Exception exception)
					{
						logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/ExportAll");
					}
				}
			}
		}

		public void Export<T>(T entity)
		{
			using (var db = new LiteDatabase(connectionString))
			{
				try
				{
					string collectionName = typeof(T).Name.ToLower();
					string exportFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.collection");
					File.AppendAllText(exportFileName, JsonSerializer.Serialize(new BsonValue(db.GetCollection(collectionName).FindAll())));
				}
				catch (Exception exception)
				{
					logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Export");
				}
			}
		}

		public void ImportAll()
		{
			using (var db = new LiteDatabase(connectionString))
			{
				foreach (string collectionName in db.GetCollectionNames())
				{
					string backupFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.backup");
					string exportFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.collection");

					if (File.Exists(exportFileName) == true)
					{
						bool isDelete = false;
						try
						{
							File.AppendAllText(backupFileName, JsonSerializer.Serialize(new BsonValue(db.GetCollection(collectionName).FindAll())));
							db.GetCollection(collectionName).DeleteAll();
							isDelete = true;

							var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(exportFileName));
							List<BsonDocument> bsonDocuments = new List<BsonDocument>();
							foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
							{
								bsonDocuments.Add((BsonDocument)bsonValue);
							}
							db.GetCollection(collectionName).InsertBulk(bsonDocuments);
						}
						catch (Exception exception)
						{
							logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/ImportAll");

							if (isDelete == true && File.Exists(backupFileName) == true)
							{
								var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(backupFileName));
								List<BsonDocument> bsonDocuments = new List<BsonDocument>();
								foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
								{
									bsonDocuments.Add((BsonDocument)bsonValue);
								}
								db.GetCollection(collectionName).InsertBulk(bsonDocuments);
							}
						}
						finally
						{
							File.Delete(backupFileName);
						}
					}
				}
			}
		}

		public void Import<T>(T entity)
		{
			using (var db = new LiteDatabase(connectionString))
			{
				string collectionName = typeof(T).Name.ToLower();
				string backupFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.backup");
				string exportFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.collection");

				if (File.Exists(exportFileName) == true)
				{
					bool isDelete = false;
					try
					{
						File.AppendAllText(backupFileName, JsonSerializer.Serialize(new BsonValue(db.GetCollection(collectionName).FindAll())));
						db.GetCollection(collectionName).DeleteAll();
						isDelete = true;

						var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(exportFileName));
						List<BsonDocument> bsonDocuments = new List<BsonDocument>();
						foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
						{
							bsonDocuments.Add((BsonDocument)bsonValue);
						}
						db.GetCollection(collectionName).InsertBulk(bsonDocuments);
					}
					catch (Exception exception)
					{
						logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/ImportAll");

						if (isDelete == true && File.Exists(backupFileName) == true)
						{
							var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(backupFileName));
							List<BsonDocument> bsonDocuments = new List<BsonDocument>();
							foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
							{
								bsonDocuments.Add((BsonDocument)bsonValue);
							}
							db.GetCollection(collectionName).InsertBulk(bsonDocuments);
						}
					}
					finally
					{
						File.Delete(backupFileName);
					}
				}
			}
		}
	}
}
