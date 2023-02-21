using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using Foxite.Common;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FridgeBot {
	public sealed class Program {
		public static IHost Host { get; set; }

		private static IHostBuilder CreateHostBuilder(string[] args) =>
			Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
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
				.ConfigureLogging((_, builder) => {
					builder.AddExceptionDemystifyer();
				})
				.ConfigureServices((hbc, isc) => {
					//isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
					isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
					
					isc.AddSingleton(isp => {
						var config = new DiscordConfiguration {
							Token = hbc.Configuration.GetSection("Discord").GetValue<string>("Token"),
							Intents = DiscordIntents.GuildMessages | DiscordIntents.GuildMembers | DiscordIntents.GuildMessageReactions | DiscordIntents.Guilds,
							LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
							MinimumLogLevel = LogLevel.Information,
							MessageCacheSize = 0
						};
						return new DiscordClient(config);
					});

					isc.AddSingleton<HttpClient>();
					
					isc.ConfigureDbContext<FridgeDbContext>();
					
					isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));
				})
				.Build();

			Host = host;

			await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
				await dbContext.Database.MigrateAsync();
			}

			var discord = host.Services.GetRequiredService<DiscordClient>();
			var commands = discord.UseCommandsNext(new CommandsNextConfiguration() {
				EnableMentionPrefix = true,
				EnableDefaultHelp = true,
				EnableDms = false,
				Services = host.Services,
			});
			commands.RegisterCommands<AdminModule>();
			commands.CommandErrored += async (_, ea) => {
				switch (ea.Exception) {
					case CommandNotFoundException:
						await ea.Context.RespondAsync("Command not found");
						return;
					case ChecksFailedException:
						await ea.Context.RespondAsync("Checks failed 🙁");
						return;
					case ArgumentException { Message: "Could not find a suitable overload for the command." }:
						await ea.Context.RespondAsync("Invalid arguments.");
						return;
				}

				string N(object? o) => o?.ToString() ?? "null";
				string errorMessage =
					$"Exception in OnMessageCreated\n" +
					$"author: {N(ea.Context.User?.Id)} ({N(ea.Context.User?.Username)}#{N(ea.Context.User?.Discriminator)}), bot: {N(ea.Context.User?.IsBot)}\n" +
					$"message: {N(ea.Context.Message?.Id)} ({N(ea.Context.Message?.JumpLink)}), type: {N(ea.Context.Message?.MessageType?.ToString() ?? "(null)")}, webhook: {N(ea.Context.Message?.WebhookMessage)}\n" +
					$"channel {N(ea.Context.Channel?.Id)} ({N(ea.Context.Channel?.Name)})\n" +
					$"{(ea.Context.Channel?.Guild != null ? $"guild {N(ea.Context.Channel?.Guild?.Id)} ({N(ea.Context.Channel?.Guild?.Name)})" : "")}";
				Host.Services.GetRequiredService<ILogger<Program>>().LogCritical(ea.Exception, errorMessage);
				await ea.Context.RespondAsync("Internal error, devs notified.");
				
				await Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync(errorMessage, ea.Exception.Demystify());
			};

			discord.MessageReactionAdded += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, true);
			discord.MessageReactionRemoved += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, false);

			discord.ClientErrored += (_, eventArgs) => Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}
	}
}
