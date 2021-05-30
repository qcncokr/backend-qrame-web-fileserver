using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Qrame.CoreFX.Helper;
using Qrame.Web.FileServer.Extensions;

using Serilog;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Qrame.Web.FileServer
{
    public class Program
	{
		private static System.Timers.Timer startupAwaitTimer;
		private static bool isDebuggerAttach = false;
		private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		public static async Task Main(string[] args)
		{
			try
			{
				ArgumentHelper arguments = new ArgumentHelper(args);
				var environmentName = Environment.GetEnvironmentVariable("QRAME_ENVIRONMENT");
				if (string.IsNullOrEmpty(environmentName) == true)
				{
					environmentName = "";
				}

				IConfigurationRoot configuration = null;
				var configurationBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

				string environmentFileName = $"appsettings.{environmentName}.json";
				if (File.Exists(Path.Combine(Environment.CurrentDirectory, environmentFileName)) == true)
				{
					configuration = configurationBuilder.AddJsonFile(environmentFileName).Build();
				}
				else
				{
					configuration = configurationBuilder.Build();
				}

				Log.Logger = new LoggerConfiguration()
					.ReadFrom.Configuration(configuration)
					.CreateLogger();

				Log.Fatal("Program Startup - QRAME_ENVIRONMENT: ", environmentName);
				Log.Verbose("");
				Log.Debug("");
				Log.Information("");
				Log.Warning("");
				Log.Error("");
				Log.Fatal("");

				Log.Information($"Bootstrapping IConfigurationRoot Qrame.Web.FileServer... {StaticConfig.BootstrappingVariables(configuration).ToString()}");

				if (arguments["debug"] != null)
				{
					int startupAwaitDelay = 0;
					if (arguments["delay"] != null)
					{
					    startupAwaitDelay = int.Parse(arguments["delay"].ToString());
					}

					startupAwaitTimer = new System.Timers.Timer(1000);
					startupAwaitTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
					{
						if (isDebuggerAttach == true)
						{
							startupAwaitTimer.Stop();
							cancellationTokenSource.Cancel();
						}
					};
					startupAwaitTimer.Start();

					try
					{
						await Task.Delay(startupAwaitDelay, cancellationTokenSource.Token);
					}
					catch
					{
					}
				}

				var host = CreateWebHostBuilder(args).Build();

				using (var scope = host.Services.CreateScope())
				{
					var services = scope.ServiceProvider;
					var config = services.GetService<IConfiguration>();
					var environment = services.GetService<IWebHostEnvironment>();

					Log.Information($"Bootstrapping IWebHostEnvironment Qrame.Web.FileServer... {StaticConfig.BootstrappingVariables(environment).ToString()}");
				}

				host.Run();
			}
			catch (Exception exception)
			{
				Log.Fatal(exception, "Unable to bootstrap Qrame.Web.FileServer");
				Console.WriteLine(exception);
			}

			Log.CloseAndFlush();
		}

		public static IHostBuilder CreateWebHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.UseContentRoot(Directory.GetCurrentDirectory())
				.UseSerilog((context, config) =>
				{
					config.ReadFrom.Configuration(context.Configuration);
				})
				.ConfigureServices((context, services) =>
				{
					services.Configure<KestrelServerOptions>(context.Configuration.GetSection("Kestrel"));
				})
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>();
					webBuilder.UseKestrel((options) =>
					{
						options.AddServerHeader = false;
					});
				})
				.UseWindowsService()
				.UseSystemd();
		}
	}
}
