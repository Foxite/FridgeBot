using DSharpPlus.Entities;

namespace FridgeBot;

/// <summary>
/// An interface for <see cref="DiscordMessage"/> so that we can mock it.
/// </summary>
public interface IDiscordMessage {
	bool AuthorIsCurrent { get; }
	ulong? GuildId { get; }
	ulong ChannelId { get; }
	ulong Id { get; }
	DateTimeOffset CreationTimestamp { get; }
	IReadOnlyCollection<IDiscordReaction> Reactions { get; }
}

public class RealDiscordMessage : IDiscordMessage {
	public DiscordMessage Message { get; }

	public bool AuthorIsCurrent => Message.Author.IsCurrent;
	public ulong? GuildId => Message.Channel.GuildId;
	public ulong ChannelId => Message.ChannelId;
	public ulong Id => Message.Id;
	public DateTimeOffset CreationTimestamp => Message.CreationTimestamp;
	public IReadOnlyCollection<IDiscordReaction> Reactions => Message.Reactions.CollectionSelect(reaction => new RealDiscordReaction(reaction));

	public RealDiscordMessage(DiscordMessage message) {
		Message = message;
	}
}
