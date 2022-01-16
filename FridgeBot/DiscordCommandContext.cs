using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot {
	public class DiscordCommandContext : CommandContext {
		public DiscordMessage Message { get; }
		public DiscordChannel Channel { get; }
		public DiscordGuild Guild { get; }
		public DiscordMember User { get; }

		public DiscordCommandContext(IServiceProvider serviceProvider, DiscordMessage message) : base(serviceProvider) {
			Message = message;
			Channel = message.Channel;
			Guild = message.Channel.Guild;
			User = (DiscordMember) message.Author;
		}
	}
}
