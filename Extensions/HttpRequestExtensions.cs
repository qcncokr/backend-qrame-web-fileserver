using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Qrame.Web.FileServer.Extensions
{
	public static class HttpRequestExtensions
	{
		public static async Task<string> GetRawBodyStringAsync(this HttpRequest request, Encoding encoding = null, Stream inputStream = null)
		{
			if (encoding == null)
			{
				encoding = Encoding.UTF8;
			}

			if (inputStream == null)
			{
				inputStream = request.Body;
			}

			using (StreamReader reader = new StreamReader(inputStream, encoding))
			{
				return await reader.ReadToEndAsync();
			}
		}

		public static async Task<byte[]> GetRawBodyBytesAsync(this HttpRequest request, Stream inputStream = null)
		{
			if (inputStream == null)
			{
				inputStream = request.Body;
			}

			using (var ms = new MemoryStream(8192))
			{
				await inputStream.CopyToAsync(ms);
				return ms.ToArray();
			}
		}

		public static string GetUrlAuthority(this HttpRequest request)
		{
			var builder = new UriBuilder();
			builder.Scheme = request.Scheme;
			builder.Host = request.Host.Value;
			builder.Path = request.Path;
			return builder.Uri.ToString();
		}

		public static int GetInt(this string value)
		{
			int result;
			if (int.TryParse(value, out result) == true)
			{
				return result;
			}
			else
			{
				return 0;
			}
		}

		public static long GetLong(this string value)
		{
			long result;
			if (long.TryParse(value, out result) == true)
			{
				return result;
			}
			else
			{
				return 0;
			}
		}
	}
}
