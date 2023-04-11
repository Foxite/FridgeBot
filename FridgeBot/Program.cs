using System.Diagnostics;
using System.Reflection;
using Foxite.Common;
using Foxite.Common.Notifications;
using FridgeBot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;
using Revcord;
using Revcord.Commands;
using Revcord.Discord;

using IHost host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration((hostingContext, configuration) => {
		configuration.Sources.Clear();

		configuration
			.AddJsonFile("appsettings.json", true, true)
			.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true)
			.AddEnvironmentVariables("FRIDGE_")
			.AddCommandLine(args);
	})
	.ConfigureLogging((_, builder) => {
		builder.AddExceptionDemystifyer();
	})
	.ConfigureServices((hbc, isc) => {
		//isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
		isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
			
		isc.AddSingleton(isp => {
			var config = new DSharpPlus.DiscordConfiguration {
				Token = hbc.Configuration.GetSection("Discord").GetValue<string>("Token"),
				Intents = DSharpPlus.DiscordIntents.GuildMessages | DSharpPlus.DiscordIntents.GuildMembers | DSharpPlus.DiscordIntents.GuildMessageReactions | DSharpPlus.DiscordIntents.Guilds,
				LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
				MinimumLogLevel = LogLevel.Information,
				MessageCacheSize = 0
			};
			return (ChatClient) new DiscordChatClient(config);
		});

		isc.AddSingleton<HttpClient>();
		isc.AddScoped<FridgeService>();
		isc.AddSingleton<IFridgeTarget, RevcordFridgeTarget>();
			
		isc.ConfigureDbContext<FridgeDbContext>();
			
		isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));

		isc.AddSingleton<CommandService>();
	})
	.Build();
	
await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
	await dbContext.Database.MigrateAsync();
}

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var notifications = host.Services.GetRequiredService<NotificationService>();
var chat = host.Services.GetRequiredService<ChatClient>();
	
async Task HandleHandlerException(string name, Exception exception, DiscordMessage? message) {
	string N(object? o) => o?.ToString() ?? "null";
	FormattableString errorMessage =
		@$"Exception in {name}
			   message: {N(message?.Id)} ({N(message?.JumpLink)}), is system: {N(message?.IsSystemMessage.ToString() ?? "(null)")}
			   author: {N(message?.Author?.Id)} ({N(message?.Author?.DiscriminatedUsername)}), bot: {N(message?.Author?.IsBot)}
			   channel {N(message?.Channel?.Id)} ({N(message?.Channel?.Name)})
			   {(message?.Guild != null ? $"guild {N(message?.Guild?.Id)} ({N(message?.Guild?.Name)})" : "")}";
	
	logger.LogCritical(exception, errorMessage);
	await notifications.SendNotificationAsync(errorMessage, exception.Demystify());
}

var commands = host.Services.GetRequiredService<CommandService>();
commands.AddModules(Assembly.GetExecutingAssembly());

chat.MessageCreated += async (args) => {
	if (args.Message.Content != null && args.Message.Content.StartsWith(chat.CurrentUser.MentionString)) {
		IResult result = await commands.ExecuteAsync(args.Message.Content[chat.CurrentUser.MentionString.Length..], new RevcordCommandContext(args.Message, host.Services));
		if (result is not SuccessfulResult) {
			string? responseMessage;
			if (result is ChecksFailedResult cfr) {
				responseMessage = string.Join("\n", cfr.FailedChecks.Select(tuple => tuple.Result).Where(cr => !cr.IsSuccessful).Select(cr => $"- {cr.FailureReason}"));
			} else {
				responseMessage = result.ToString();
			}

			if (responseMessage != null) {
				await args.Message.SendReplyAsync(responseMessage);
			}
		}
	}
};

async Task OnReactionModifiedAsync(ReactionModifiedArgs args) {
	var message = args.Message;
	// Acquire additional data such as the author, and refresh reaction counts
	try {
		message = await message.Channel.GetMessageAsync(message.Id);
	} catch (EntityNotFoundException) {
		// Message was deleted since the event fired
		return;
	}
	if (message == null) {
		// Also deleted? idk. better safe than sorry
		return;
	}

	await using var scope = host.Services.CreateAsyncScope();
	var fridgeService = scope.ServiceProvider.GetRequiredService<FridgeService>();
	await fridgeService.ProcessReactionAsync(message);
}

chat.ReactionAdded += OnReactionModifiedAsync;
chat.ReactionRemoved += OnReactionModifiedAsync;

chat.EventHandlerError += (eventArgs) => notifications.SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
chat.ClientError += eventArgs => {
	logger.LogError(eventArgs.Exception, "Client error");
	return Task.CompletedTask;
};
	
await chat.StartAsync();

await host.RunAsync();
