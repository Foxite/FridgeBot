using System.Diagnostics;
using System.Reflection;
using Foxite.Common;
using Foxite.Common.Notifications;
using FridgeBot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using Revcord;
using Revcord.Commands;
using Revcord.Discord;
using Revcord.Revolt;

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
		isc.Configure<DiscordConfig>(hbc.Configuration.GetSection("Discord"));
		isc.Configure<RevoltConfig>(hbc.Configuration.GetSection("Revolt"));
		
		isc.TryAddEnumerable(ServiceDescriptor.Singleton<ChatClient, DiscordChatClient>(isp => new DiscordChatClient(new DSharpPlus.DiscordConfiguration {
			Token = isp.GetRequiredService<IOptions<DiscordConfig>>().Value.Token,
			Intents = DSharpPlus.DiscordIntents.GuildMessages | DSharpPlus.DiscordIntents.GuildMembers | DSharpPlus.DiscordIntents.GuildMessageReactions | DSharpPlus.DiscordIntents.Guilds,
			LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
			MinimumLogLevel = LogLevel.Information,
			MessageCacheSize = 0
		})));

		isc.TryAddEnumerable(ServiceDescriptor.Singleton<ChatClient, RevoltChatClient>(isp => new RevoltChatClient(isp.GetRequiredService<IOptions<RevoltConfig>>().Value.Token)));

			
		isc.AddSingleton<ChatClientService>();

		isc.AddSingleton<HttpClient>();
		isc.TryAddEnumerable(ServiceDescriptor.Transient<IFridgeTarget, RevoltFridgeTarget>());
		isc.TryAddEnumerable(ServiceDescriptor.Transient<IFridgeTarget, DiscordFridgeTarget>());
		isc.AddScoped<FridgeService>();
		
		isc.ConfigureDbContext<FridgeDbContext>();
			
		isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));

		isc.AddRevcordCommands();
	})
	.Build();
	
await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
	// TODO apply multi stage migration later
	await dbContext.Database.EnsureCreatedAsync();
	//await dbContext.Database.MigrateAsync();
}

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var notifications = host.Services.GetRequiredService<NotificationService>();
var chat = host.Services.GetRequiredService<ChatClientService>();

async Task HandleHandlerException(HandlerErrorArgs args) {
	string N(object? o) => o?.ToString() ?? "null";
	FormattableString errorMessage =
		@$"Exception in {args.EventName}";
	/*
			   message: {N(message?.Id)} ({N(message?.JumpLink)}), is system: {N(message?.IsSystemMessage.ToString() ?? "(null)")}
			   author: {N(message?.Author?.Id)} ({N(message?.Author?.DiscriminatedUsername)}), bot: {N(message?.Author?.IsBot)}
			   channel {N(message?.Channel?.Id)} ({N(message?.Channel?.Name)})
			   {(message?.Guild != null ? $"guild {N(message?.Guild?.Id)} ({N(message?.Guild?.Name)})" : "")}
	*/
	
	logger.LogCritical(args.Exception, errorMessage);
	await notifications.SendNotificationAsync(errorMessage, args.Exception.Demystify());
}

var commands = host.Services.GetRequiredService<CommandService>();
commands.AddModules(Assembly.GetExecutingAssembly());

chat.MessageCreated += async (args) => {
	if (args.Message.Content != null && args.Message.Content.StartsWith(args.Client.CurrentUser.MentionString)) {
		IResult result = await commands.ExecuteAsync(args.Message.Content[args.Client.CurrentUser.MentionString.Length..], new RevcordCommandContext(args.Message, host.Services));
		if (result is not SuccessfulResult) {
			Console.WriteLine(result.GetType().FullName);
			string? responseMessage;
			switch (result) {
				case ChecksFailedResult cfr:
					responseMessage = string.Join("\n", cfr.FailedChecks.Select(tuple => tuple.Result).Where(cr => !cr.IsSuccessful).Select(cr => $"- {cr.FailureReason}"));
					break;
				case CommandExecutionFailedResult cefr:
					Console.WriteLine($"Execution failed at {cefr.CommandExecutionStep}: {cefr.Exception.ToStringDemystified()}");
					responseMessage = result.ToString();
					break;
				default:
					responseMessage = result.ToString();
					break;
			}

			if (responseMessage != null) {
				await args.Message.SendReplyAsync(responseMessage);
			}
		}
	}
};

async Task OnReactionModifiedAsync(ReactionModifiedArgs args) {
	await using var scope = host.Services.CreateAsyncScope();
	var fridgeService = scope.ServiceProvider.GetRequiredService<FridgeService>();
	await fridgeService.ProcessReactionAsync(args.Message);
}

// TODO Run these synchronously, because otherwise you'll get concurrent handlers that will end up trying to create a new FridgeEntry
chat.ReactionAdded += OnReactionModifiedAsync;
chat.ReactionRemoved += OnReactionModifiedAsync;

chat.EventHandlerError += HandleHandlerException;

chat.ClientError += eventArgs => {
	logger.LogError(eventArgs.Exception, "Client error");
	return Task.CompletedTask;
};
	
await chat.StartAsync();

await host.RunAsync();
