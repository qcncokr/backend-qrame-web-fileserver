using LiteDB;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;

namespace Qrame.Web.FileServer.Extensions
{
	public interface ILiteDBClient
	{
		bool Insert<T>(T entity);

		int Inserts<T>(List<T> entity);

		bool Update<T>(T entity);

		int Updates<T>(List<T> entity);

		bool Upsert<T>(T entity);

		int Upserts<T>(List<T> entity);

		List<T> Select<T>(Expression<Func<T, bool>> filter, Expression<Func<T, string>> ensureIndex = null, int skip = 0, int limit = int.MaxValue);

		int Delete<T>(Expression<Func<T, bool>> filter = null);

		bool SetFileStorage(string key, string filePath, Stream stream = null);

		LiteFileInfo<string> GetFileStorage(string key, Stream stream);

		void ExportAll();

		void Export<T>(T entity);

		void ImportAll();

		void Import<T>(T entity);
	}
}
