using System;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Qrame.Web.FileServer.Message;
using Serilog;

namespace Qrame.Web.FileServer.Extensions
{
	public class RawRequestBodyFormatter : InputFormatter
	{
		private ILogger logger { get; }

		public RawRequestBodyFormatter(ILogger logger)
		{
			this.logger = logger;

			SupportedMediaTypes.Add(new MediaTypeHeaderValue("qrame/plain-message"));
			SupportedMediaTypes.Add(new MediaTypeHeaderValue("qrame/json-message"));
			SupportedMediaTypes.Add(new MediaTypeHeaderValue("qrame/stream-message"));
		}

		public override bool CanRead(InputFormatterContext context)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context));
			}

			var contentType = context.HttpContext.Request.ContentType;
			if (string.IsNullOrEmpty(contentType) == false && (contentType.IndexOf("qrame/plain-message") > -1 || contentType.IndexOf("qrame/json-message") > -1 || contentType.IndexOf("qrame/stream-message") > -1))
			{
				return true;
			}

			return false;
		}

		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
		{
			var request = context.HttpContext.Request;
			var contentType = context.HttpContext.Request.ContentType;

			if (string.IsNullOrEmpty(contentType) == false)
			{
				string modelType = context.ModelType.Name;
				try
				{
					if (modelType == "DownloadRequest")
					{
						if (contentType.IndexOf("qrame/plain-message") > -1 || contentType.IndexOf("qrame/json-message") > -1)
						{
							using (var reader = new StreamReader(request.Body))
							{
								var content = await reader.ReadToEndAsync();
								return await InputFormatterResult.SuccessAsync(JsonConvert.DeserializeObject<DownloadRequest>(content));
							}
						}
						else if (contentType.IndexOf("qrame/stream-message") > -1)
						{
							using (var ms = new MemoryStream(2048))
							{
								await request.Body.CopyToAsync(ms);
								var content = ms.ToArray();
								return await InputFormatterResult.SuccessAsync(MessagePackSerializer.Deserialize<DownloadRequest>(content));
							}
						}
					}
				}
				catch (Exception exception)
				{
					logger.Error("[{LogCategory}] " + exception.ToMessage(), "ReadRequestBodyAsync");
				}
			}

			return await InputFormatterResult.FailureAsync();
		}
	}
}
