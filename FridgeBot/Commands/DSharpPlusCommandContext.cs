using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace FridgeBot;

public class DSharpPlusCommandContext : CommandContext {
	public DiscordMessage Message { get; }
	public DiscordChannel Channel { get; }
	public DiscordGuild? Guild { get; }
	public DiscordUser User { get; }
	public DiscordClient Client { get; }

	public DSharpPlusCommandContext(DiscordMessage message, IServiceProvider serviceProvider) : base(serviceProvider) {
		Message = message;
		Channel = message.Channel;
		Guild = message.Channel.Guild;
		User = message.Author;
		Client = serviceProvider.GetRequiredService<DiscordClient>();
	}

	public Task<DiscordMessage> RespondAsync(string message) {
		return Message.RespondAsync(message);
	}
}
