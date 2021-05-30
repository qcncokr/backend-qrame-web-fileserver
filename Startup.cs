using MessagePack.AspNetCoreMvcFormatter;
using MessagePack.Resolvers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Qrame.Core.Library.ApiClient;
using Qrame.CoreFX.ExtensionMethod;
using Qrame.Web.FileServer.Entities;
using Qrame.Web.FileServer.Extensions;

using Serilog;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Qrame.Web.FileServer
{
    public class Startup
	{
		private string startTime = null;
		private int processID = 0;
		bool useProxyForward = false;
		bool useResponseComression = false;
		private IConfiguration configuration { get; }
		private IWebHostEnvironment environment { get; }
		static readonly ServerEventListener serverEventListener = new ServerEventListener();

		public Startup(IWebHostEnvironment environment, IConfiguration configuration)
		{
			Process currentProcess = Process.GetCurrentProcess();
			processID = currentProcess.Id;
			startTime = currentProcess.StartTime.ToString();

			this.configuration = configuration;
			this.environment = environment;
			this.useProxyForward = bool.Parse(configuration.GetSection("AppSettings")["UseForwardProxy"]);
			this.useResponseComression = bool.Parse(configuration.GetSection("AppSettings")["UseResponseComression"]);
		}

		public void ConfigureServices(IServiceCollection services)
		{
			var appSettings = configuration.GetSection("AppSettings");
			StaticConfig.ApplicationName = environment.ApplicationName;
			StaticConfig.ContentRootPath = environment.ContentRootPath;
			StaticConfig.EnvironmentName = environment.EnvironmentName;
			StaticConfig.WebRootPath = environment.WebRootPath;
			StaticConfig.BusinessServerUrl = appSettings["BusinessServerUrl"].ToString();
			StaticConfig.RepositoryList = appSettings["RepositoryList"].ToString();
			StaticConfig.XFrameOptions = appSettings["XFrameOptions"].ToString();
			StaticConfig.AllowMaxFileUploadLength = long.Parse(appSettings["AllowMaxFileUploadLength"].ToString());
			StaticConfig.TransactionFileRepositorys = appSettings["TransactionFileRepositorys"].ToString();
			StaticConfig.PurgeTokenTimeout = int.Parse(appSettings["PurgeTokenTimeout"].ToString());
			StaticConfig.TokenGenerateIPCheck = bool.Parse(appSettings["TokenGenerateIPCheck"].ToString());
			StaticConfig.IsLocalDB = bool.Parse(appSettings["IsLocalDB"].ToString());
			StaticConfig.RunningEnvironment = appSettings["RunningEnvironment"].ToString();
			StaticConfig.HostName = appSettings["HostName"].ToString();
			StaticConfig.SystemCode = appSettings["SystemCode"].ToString();
			StaticConfig.IsExceptionDetailText = bool.Parse(appSettings["IsExceptionDetailText"].ToString());
			StaticConfig.WithOrigins = appSettings["WithOrigins"].ToString();

			StaticConfig.IsApiFindServer = bool.Parse(appSettings["IsApiFindServer"].ToString());
			TransactionConfig.ApiFindUrl = appSettings["ApiFindUrl"].ToString();
			TransactionConfig.DomainServerType = appSettings["DomainServerType"].ToString();
			TransactionConfig.Transaction.SystemID = appSettings["TransactionSystemID"].ToString();
			TransactionConfig.Transaction.SystemCode = appSettings["SystemCode"].ToString();
			TransactionConfig.Transaction.RunningEnvironment = appSettings["RunningEnvironment"].ToString();
			TransactionConfig.Transaction.ProtocolVersion = appSettings["ProtocolVersion"].ToString();

			// 거래서버 경로 캐시 생성
			TransactionClient apiClient = new TransactionClient();
			if (StaticConfig.IsApiFindServer == true)
			{
				apiClient.AddFindService(TransactionConfig.Transaction.SystemID, TransactionConfig.DomainServerType);
			}
			else
			{
				var domainAPIServer = new JObject();
				domainAPIServer.Add("ExceptionText", null);
				domainAPIServer.Add("RequestID", "");
				domainAPIServer.Add("ServerID", appSettings["DomainAPIServer:ServerID"].ToString());
				domainAPIServer.Add("ServerType", appSettings["DomainAPIServer:ServerType"].ToString());
				domainAPIServer.Add("Protocol", appSettings["DomainAPIServer:Protocol"].ToString());
				domainAPIServer.Add("IP", appSettings["DomainAPIServer:IP"].ToString());
				domainAPIServer.Add("Port", appSettings["DomainAPIServer:Port"].ToString());
				domainAPIServer.Add("Path", appSettings["DomainAPIServer:Path"].ToString());
				domainAPIServer.Add("ClientIP", appSettings["DomainAPIServer:ClientIP"].ToString());
				StaticConfig.DomainAPIServer = domainAPIServer;

				apiClient.AddApiService(TransactionConfig.Transaction.SystemID, TransactionConfig.DomainServerType, StaticConfig.DomainAPIServer);
			}

			StaticConfig.IsConfigure = true;

			try
			{
				if (StaticConfig.IsLocalDB == true)
				{
					LiteDBClient liteDBClient = new LiteDBClient(Log.Logger, configuration);
					liteDBClient.Delete<Repository>();

					string repositoryFilePath = Path.Combine(StaticConfig.ContentRootPath, "repository.json");
					if (File.Exists(repositoryFilePath) == true)
					{
						try
						{
							string repository = File.ReadAllText(repositoryFilePath);
							StaticConfig.FileRepositorys = JsonConvert.DeserializeObject<List<Repository>>(repository);
						}
						catch (Exception exception)
						{
							Log.Fatal("[{LogCategory}] " + $"{repositoryFilePath}: 저장소 파일 확인 필요 " + exception.ToMessage(), "Startup/ConfigureServices");
							throw;
						}
					}

					liteDBClient.Inserts(StaticConfig.FileRepositorys);
				}
				else
				{
					BusinessApiClient businessApiClient = new BusinessApiClient(Log.Logger);
					var task = Task.Run(async () =>
					{
						StaticConfig.FileRepositorys = await businessApiClient.GetRepositorys(StaticConfig.RepositoryList);
					});

					task.Wait();

					if (StaticConfig.FileRepositorys == null || StaticConfig.FileRepositorys.Count == 0)
					{
						string message = "저장소 정보가 없으면 서버 기동 안함";
						Log.Fatal("[{LogCategory}] " + message, "Startup/ConfigureServices");
						throw new Exception(message);
					}
				}
			}
			catch (Exception exception)
			{
				Log.Error("[{LogCategory}] " + "repository.json 환경 설정 오류 " + exception.ToMessage(), "Startup/ConfigureServices");
				throw;
			}

			try
			{
				StaticConfig.FileRootPath = appSettings["FileRootPath"].ToString();
				if (Directory.Exists(StaticConfig.FileRootPath) == false)
				{
					Directory.CreateDirectory(StaticConfig.FileRootPath);
				}
			}
			catch (Exception exception)
			{
				Log.Error("[{LogCategory}] " + $"{StaticConfig.FileRootPath} 경로 확인 필요 " + exception.ToMessage(), "Startup/ConfigureServices");
				throw;
			}

			if (useResponseComression == true)
			{
				services.AddResponseCompression(options =>
				{
					options.EnableForHttps = bool.Parse(configuration.GetSection("AppSettings")["ComressionEnableForHttps"]);
					options.Providers.Add<BrotliCompressionProvider>();
					options.Providers.Add<GzipCompressionProvider>();

					List<string> mimeTypes = new List<string>();
					var comressionMimeTypes = configuration.GetSection("AppSettings").GetSection("ComressionMimeTypes").AsEnumerable();
					foreach (var comressionMimeType in comressionMimeTypes)
					{
						mimeTypes.Add(comressionMimeType.Value);
					}

					options.MimeTypes = mimeTypes;
				});
			}

			if (useProxyForward == true)
			{
				services.Configure<ForwardedHeadersOptions>(options =>
				{
					var forwards = configuration.GetSection("AppSettings").GetSection("ForwardProxyIP").AsEnumerable();
					foreach (var item in forwards)
					{
						if (string.IsNullOrEmpty(item.Value) == false)
						{
							options.KnownProxies.Add(IPAddress.Parse(item.Value));
						}
					}

					bool useSameIPProxy = bool.Parse(configuration.GetSection("AppSettings")["UseSameIPProxy"]);
					if (useSameIPProxy == true)
					{
						IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
						foreach (IPAddress ipAddress in host.AddressList)
						{
							if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
							{
								options.KnownProxies.Add(ipAddress);
							}
						}
					}
				});
			}

			ClientSessionManager.PurgeTokenTimeout = StaticConfig.PurgeTokenTimeout;

			services.Configure<FormOptions>(options =>
			{
				options.ValueLengthLimit = int.MaxValue;
				options.MultipartHeadersLengthLimit = int.MaxValue;
				options.MultipartBodyLengthLimit = StaticConfig.AllowMaxFileUploadLength;
			});

			services.Configure<IISServerOptions>(options =>
			{
				options.MaxRequestBodySize = StaticConfig.AllowMaxFileUploadLength;
			});

			services.AddCors(options =>
			{
				List<string> withOrigins = new List<string>();
				if (string.IsNullOrEmpty(StaticConfig.WithOrigins) == false) {
                    foreach (string item in StaticConfig.WithOrigins.Split(","))
                    {
						withOrigins.Add(item.Trim());
					}
				}
			
				options.AddDefaultPolicy(
				builder => builder
					.AllowAnyHeader()
					.AllowAnyMethod()
					.WithOrigins(withOrigins.ToArray())
					.SetIsOriginAllowedToAllowWildcardSubdomains()
				);
			});

			services.AddMvc().AddMvcOptions(option =>
			{
				option.EnableEndpointRouting = false;

				option.InputFormatters.Insert(0, new MessagePackInputFormatter(ContractlessStandardResolver.Options));
				option.InputFormatters.Insert(0, new RawRequestBodyFormatter(Log.Logger));
			})
			.AddJsonOptions(jsonOptions =>
			{
				jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
			});

			services.AddDistributedMemoryCache();
			services.AddSession(options =>
			{
				options.IdleTimeout = TimeSpan.FromHours(1);
				options.Cookie.IsEssential = true;
			});

			services.AddSingleton(Log.Logger);
			services.AddSingleton(configuration);
			services.AddSingleton<ILiteDBClient, LiteDBClient>();

			IFileProvider physicalProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
			services.AddSingleton<IFileProvider>(physicalProvider);
			services.AddControllers();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (useResponseComression == true)
			{
				app.UseResponseCompression();
			}

			if (useProxyForward == true)
			{
				app.UseForwardedHeaders(new ForwardedHeadersOptions
				{
					ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
				});
			}

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseStaticFiles(new StaticFileOptions
			{
				ServeUnknownFileTypes = true,
				DefaultContentType = "text/plain"
			});

            foreach (Repository item in StaticConfig.FileRepositorys)
            {
				if (item.StorageType == "FileSystem" && item.IsVirtualPath.ParseBool() == true)
				{
					if (Directory.Exists(item.PhysicalPath) == false)
					{
						Directory.CreateDirectory(item.PhysicalPath);
					}

					app.UseStaticFiles(new StaticFileOptions
					{
						ServeUnknownFileTypes = true,
						DefaultContentType = "text/plain",
						FileProvider = new PhysicalFileProvider(item.PhysicalPath),
						RequestPath = "/" + item.RepositoryID
					});
				}
            }

			app.UseSession();
			app.UseCors();
			app.UseSerilogRequestLogging();
			app.UseRouting();
			app.UseAuthorization();
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapGet("/diagnostics", async context =>
				{
					var result = new
					{
						Environment = new
						{
							ProcessID = processID,
							StartTime = startTime,
							SystemCode = StaticConfig.SystemCode,
							ApplicationName = StaticConfig.ApplicationName,
							Is64Bit = Environment.Is64BitOperatingSystem,
							MachineName = Environment.MachineName,
							HostName = StaticConfig.HostName,
							RunningEnvironment = StaticConfig.RunningEnvironment
						},
						System = serverEventListener.SystemRuntime,
						Hosting = serverEventListener.AspNetCoreHosting,
						// Kestrel = serverEventListener.AspNetCoreServerKestrel
					};
					context.Response.Headers["Content-Type"] = "application/json";
					await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
				});
			});
			app.UseMvcWithDefaultRoute();

			try
			{
				if (env.IsProduction() == true || env.IsStaging() == true)
				{
					File.WriteAllText("appstartup-update.txt", DateTime.Now.ToString());
				}
			}
			catch
			{
			}
		}
	}
}
