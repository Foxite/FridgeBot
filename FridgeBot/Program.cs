using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FridgeBot {
	public sealed class Program {
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
					isc.AddScoped<FridgeService>();
					isc.AddSingleton<IFridgeTarget, DiscordFridgeTarget>();
					
					isc.ConfigureDbContext<FridgeDbContext>();
					
					isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));
				})
				.Build();
			
			await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
				await dbContext.Database.MigrateAsync();
			}

			var logger = host.Services.GetRequiredService<ILogger<Program>>();
			var notifications = host.Services.GetRequiredService<NotificationService>();
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
				FormattableString errorMessage =
					$@"Exception in OnMessageCreated
					author: {N(ea.Context.User.Id)} ({N(ea.Context.User.Username)}#{N(ea.Context.User.Discriminator)}), bot: {N(ea.Context.User.IsBot)}
					message: {N(ea.Context.Message.Id)} ({N(ea.Context.Message.JumpLink)}), type: {N(ea.Context.Message.MessageType?.ToString() ?? "(null)")}, webhook: {N(ea.Context.Message.WebhookMessage)}
					channel {N(ea.Context.Channel.Id)} ({N(ea.Context.Channel.Name)})
					{(ea.Context.Channel.Guild != null ? $"guild {N(ea.Context.Channel.Guild?.Id)} ({N(ea.Context.Channel.Guild?.Name)})" : "")}";
				logger.LogCritical(ea.Exception, errorMessage);
				await ea.Context.RespondAsync("Internal error, devs notified.");
				
				await notifications.SendNotificationAsync(errorMessage, ea.Exception.Demystify());
			};
			
			async Task OnReactionModifiedAsync(DiscordMessage message, DiscordEmoji emoji, bool added) {
				try {
					// Acquire additional data such as the author, and refresh reaction counts
					try {
						message = await message.Channel.GetMessageAsync(message.Id);
					} catch (NotFoundException) {
						// Message was deleted since the event fired
						return;
					}
					if (message == null) {
						// Also deleted? idk. better safe than sorry
						return;
					}

					await using var scope = host.Services.CreateAsyncScope();
					var fridgeService = scope.ServiceProvider.GetRequiredService<FridgeService>();
					await fridgeService.ProcessReactionAsync(message, emoji, added);
				} catch (Exception ex) {
					string N(object? o) => o?.ToString() ?? "null";
					FormattableString errorMessage =
						@$"Exception in OnReactionModifiedAsync
					   author: {N(message?.Author?.Id)} ({N(message?.Author?.Username)}#{N(message?.Author?.Discriminator)}), bot: {N(message?.Author?.IsBot)}
					   message: {N(message?.Id)} ({N(message?.JumpLink)}), type: {N(message?.MessageType?.ToString() ?? "(null)")}, webhook: {N(message?.WebhookMessage)}
					   channel {N(message?.Channel?.Id)} ({N(message?.Channel?.Name)})
					   {(message?.Channel?.Guild != null ? $"guild {N(message?.Channel?.Guild?.Id)} ({N(message?.Channel?.Guild?.Name)})" : "")}";
					logger.LogCritical(ex, errorMessage);
					await notifications.SendNotificationAsync(errorMessage, ex.Demystify());
				}
			}

			discord.MessageReactionAdded += (sender, ea) => _ = OnReactionModifiedAsync(ea.Message, ea.Emoji, true);
			discord.MessageReactionRemoved += (sender, ea) => _ = OnReactionModifiedAsync(ea.Message, ea.Emoji, false);

			discord.ClientErrored += (_, eventArgs) => notifications.SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}
	}
}
