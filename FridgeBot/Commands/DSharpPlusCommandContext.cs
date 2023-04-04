using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot;

public class DSharpPlusCommandContext : CommandContext {
	public DiscordMessage Message { get; }
	public DiscordChannel Channel { get; }
	public DiscordGuild? Guild { get; }
	public DiscordUser User { get; }
		
	public DSharpPlusCommandContext(DiscordMessage message, IServiceProvider serviceProvider) : base(serviceProvider) {
		Message = message;
		Channel = message.Channel;
		Guild = message.Channel.Guild;
		User = message.Author;
	}

	public Task<DiscordMessage> RespondAsync(string message) {
		return Message.RespondAsync(message);
	}
}
