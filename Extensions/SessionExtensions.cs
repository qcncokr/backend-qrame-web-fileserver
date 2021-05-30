using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Qrame.Web.FileServer.Extensions
{
	public static class SessionExtensions
	{
		// HttpContext.Session.Set<DateTime>(SessionKeyTime, currentTime);
		public static void Set<T>(this ISession session, string key, T value)
		{
			session.SetString(key, JsonConvert.SerializeObject(value));
		}

		// if (HttpContext.Session.Get<DateTime>(SessionKeyTime) == default(DateTime)) { }
		public static T Get<T>(this ISession session, string key)
		{
			var value = session.GetString(key);

			return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
		}
	}
}
