using System.Diagnostics;
using System.Reflection;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;

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
							Intents = DiscordIntents.GuildMessages | DiscordIntents.GuildMembers | DiscordIntents.GuildMessageReactions | DiscordIntents.Guilds | DiscordIntents.GuildEmojis,
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

					var notificationBuilder = isc.AddNotifications();
					if (!string.IsNullOrWhiteSpace(hbc.Configuration.GetValue<string>("DiscordNotifications:WebhookUrl"))) {
						notificationBuilder.AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));
					}

					isc.AddSingleton<CommandService>();
				})
				.Build();
			
			await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
				await dbContext.Database.MigrateAsync();
			}

			var logger = host.Services.GetRequiredService<ILogger<Program>>();
			var notifications = host.Services.GetService<NotificationService>();
			var discord = host.Services.GetRequiredService<DiscordClient>();
			
			async Task HandleHandlerException(string name, Exception exception, DiscordMessage? message) {
				string N(object? o) => o?.ToString() ?? "null";
				FormattableString errorMessage =
					@$"Exception in {name}
					   message: {N(message?.Id)} ({N(message?.JumpLink)}), type: {N(message?.MessageType?.ToString() ?? "(null)")}, webhook: {N(message?.WebhookMessage)}
					   author: {N(message?.Author?.Id)} ({N(message?.Author?.Username)}#{N(message?.Author?.Discriminator)}), bot: {N(message?.Author?.IsBot)}
					   channel {N(message?.Channel?.Id)} ({N(message?.Channel?.Name)})
					   {(message?.Channel?.Guild != null ? $"guild {N(message?.Channel?.Guild?.Id)} ({N(message?.Channel?.Guild?.Name)})" : "")}";
				logger.LogCritical(exception, errorMessage);
				if (notifications != null) {
					await notifications.SendNotificationAsync(errorMessage, exception.Demystify());
				}
			}

			var commands = host.Services.GetRequiredService<CommandService>();
			commands.AddModules(Assembly.GetExecutingAssembly());
			commands.AddTypeParser(new DiscordEmojiParser());
			commands.AddTypeParser(new DiscordChannelParser());

			discord.MessageCreated += async (sender, eventArgs) => {
				if (eventArgs.Message.Content.StartsWith(discord.CurrentUser.Mention)) {
					IResult result = await commands.ExecuteAsync(eventArgs.Message.Content.Substring(discord.CurrentUser.Mention.Length), new DSharpPlusCommandContext(eventArgs.Message, host.Services));
					if (result is not SuccessfulResult) {
						if (result is CommandExecutionFailedResult cefr) {
							logger.LogError(cefr.Exception, "Error handling command: {Reason}", cefr.FailureReason);
						}
						
						string? message;
						if (result is ChecksFailedResult cfr) {
							message = string.Join("\n", cfr.FailedChecks.Select(tuple => tuple.Result).Where(cr => !cr.IsSuccessful).Select(cr => $"- {cr.FailureReason}"));
						} else {
							message = result.ToString();
						}
						
						await eventArgs.Message.RespondAsync(message);
					}
				}
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
					await fridgeService.ProcessReactionAsync(new RealDiscordMessage(message));
				} catch (Exception ex) {
					await HandleHandlerException("OnReactionModifiedAsync", ex, message);
				}
			}

			// Run these synchronously, because otherwise you'll get concurrent handlers that will end up trying to create a new FridgeEntry
			discord.MessageReactionAdded += (sender, ea) => OnReactionModifiedAsync(ea.Message, ea.Emoji, true);
			discord.MessageReactionRemoved += (sender, ea) => OnReactionModifiedAsync(ea.Message, ea.Emoji, false);

			discord.ClientErrored += (_, eventArgs) => notifications.SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}
	}
}
