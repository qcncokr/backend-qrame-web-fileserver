using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using LiteDB;

using Microsoft.Extensions.Configuration;

using Serilog;

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

			connectionString = $"Filename={Path.Combine(StaticConfig.ContentRootPath, "fileserver.db;Mode=Shared;")}"; // Mode=Shared;
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

		public IEnumerable<T> Select<T>(Expression<Func<T, bool>> filter = null, Expression<Func<T, string>> ensureIndex = null, int skip = 0, int limit = int.MaxValue)
		{
			IEnumerable<T> result = null;

			try
			{
				using (var db = new LiteDatabase(connectionString))
				{
					var collection = db.GetCollection<T>(typeof(T).Name.ToLower());

					if (ensureIndex != null)
					{
						collection.EnsureIndex(ensureIndex);
					}

					if (filter != null)
					{
						result = collection.Find(filter, skip, limit);
					}
					else
					{
						result = collection.FindAll();
					}
				}
			}
			catch (Exception exception)
			{
				logger.Error("[{LogCategory}] " + exception.ToMessage(), "LiteDBClient/Select");
			}

			return result;
		}

		// PagedSelect<Repository>(Query.EQ("IsMultiUpload", true), "$.RepositoryName", 1);
		// PagedSelect<Repository>(Query.EQ("IsMultiUpload", true), "$.RepositoryName", -1, 1, 10);
		public IEnumerable<T> PagedSelect<T>(Query query, string orderByExpr, int order, int pageIndex = 0, int pageSize = int.MaxValue)
		{
			var tmp = "tmp_" + Guid.NewGuid().ToString().Substring(0, 5);
			var expr = new BsonExpression(orderByExpr);
			var disk = new StreamDiskService(new MemoryStream(), true);
			string collectionName = typeof(T).Name.ToLower();
			using (var db = new LiteDatabase(connectionString))
			using (var engine = new LiteEngine(disk, cacheSize: 200000))
			{
				engine.EnsureIndex(tmp, "orderBy", orderByExpr);
				engine.InsertBulk(tmp, db.Engine.Find(collectionName, query).Select(x => new BsonDocument
				{
					["_id"] = x["_id"],
					["orderBy"] = expr.Execute(x, true).First()
				}));

				var skip = pageIndex * pageSize;
				var sorted = engine.Find(tmp, Query.All("orderBy", order), skip, pageSize);
				var list = sorted.Select(x => db.Engine.FindById(collectionName, x["_id"])).Select(x => db.Mapper.ToObject<T>(x));

				return list;
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
						collection.Delete(filter);
					}
					else
					{
						collection.Delete(Query.All());
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
				using (var db = new LiteRepository(connectionString))
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

		public LiteFileInfo GetFileStorage(string key, Stream stream)
		{
			LiteFileInfo result = null;

			try
			{
				using (var db = new LiteRepository(connectionString))
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
				foreach (string collectionName in db.Engine.GetCollectionNames())
				{
					try
					{
						IEnumerable<BsonDocument> backupCollection = db.Engine.FindAll(collectionName);
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
					File.AppendAllText(exportFileName, JsonSerializer.Serialize(new BsonValue(db.Engine.FindAll(collectionName))));
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
				foreach (string collectionName in db.Engine.GetCollectionNames())
				{
					string backupFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.backup");
					string exportFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{collectionName}.collection");

					if (File.Exists(exportFileName) == true)
					{
						bool isDelete = false;
						try
						{
							File.AppendAllText(backupFileName, JsonSerializer.Serialize(new BsonValue(db.Engine.FindAll(collectionName))));
							db.Engine.Delete(collectionName, Query.All());
							isDelete = true;

							var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(exportFileName));
							List<BsonDocument> bsonDocuments = new List<BsonDocument>();
							foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
							{
								bsonDocuments.Add((BsonDocument)bsonValue);
							}
							db.Engine.InsertBulk(collectionName, bsonDocuments);
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
								db.Engine.InsertBulk(collectionName, bsonDocuments);
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
						File.AppendAllText(backupFileName, JsonSerializer.Serialize(new BsonValue(db.Engine.FindAll(collectionName))));
						db.Engine.Delete(collectionName, Query.All());
						isDelete = true;

						var bsonArray = JsonSerializer.Deserialize(File.ReadAllText(exportFileName));
						List<BsonDocument> bsonDocuments = new List<BsonDocument>();
						foreach (BsonValue bsonValue in bsonArray.AsArray.ToArray())
						{
							bsonDocuments.Add((BsonDocument)bsonValue);
						}
						db.Engine.InsertBulk(collectionName, bsonDocuments);
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
							db.Engine.InsertBulk(collectionName, bsonDocuments);
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
