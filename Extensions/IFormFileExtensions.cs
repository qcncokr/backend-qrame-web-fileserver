﻿using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.Threading.Tasks;

namespace Qrame.Web.FileServer.Extensions
{
	public static class IFormFileExtensions
	{
		public static string GetFileName(this IFormFile file)
		{
			return ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.ToString().Trim('"');
		}

		public static async Task<MemoryStream> GetFileStream(this IFormFile file)
		{
			using (MemoryStream filestream = new MemoryStream())
			{
				await file.CopyToAsync(filestream);
				return filestream;
			}
		}

		public static async Task<byte[]> GetFileArray(this IFormFile file)
		{
			using (MemoryStream filestream = new MemoryStream())
			{
				await file.CopyToAsync(filestream);
				return filestream.ToArray();
			}
		}
	}
}
