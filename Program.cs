using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using FridgeBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FridgeBot {
	public static class Program {
		private static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((hostingContext, configuration) => {
					configuration.Sources.Clear();

					configuration
						.AddJsonFile("appsettings.json", true, true)
						.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true)
						.AddEnvironmentVariables("FRIDGE_")
						.AddCommandLine(args);
				});
		
		private static async Task Main(string[] args) {
			using IHost host = CreateHostBuilder(args)
				.ConfigureServices((hbc, isc) => {
					isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
					isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
					
					isc.AddSingleton(isp => {
						var config = isp.GetRequiredService<IOptions<DiscordConfiguration>>().Value;
						config.Intents = DiscordIntents.GuildMessageReactions;
						config.LoggerFactory = isp.GetRequiredService<ILoggerFactory>();
						return new DiscordClient(config);
					});

					isc.ConfigureDbContext<FridgeDbContext>();
				})
				.Build();

			var discord = host.Services.GetRequiredService<DiscordClient>();

			discord.MessageReactionAdded += (_, e) => {
				
			}
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}
	}
}