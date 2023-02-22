using DSharpPlus.Entities;

namespace FridgeBot;

/// <summary>
/// An interface for <see cref="DiscordReaction"/> so that we can mock it.
/// </summary>
public interface IDiscordReaction {
	IDiscordEmoji Emoji { get; }
	int Count { get; }
}

public class RealDiscordReaction : IDiscordReaction {
	public DiscordReaction Reaction { get; }

	public IDiscordEmoji Emoji => new RealDiscordEmoji(Reaction.Emoji);
	public int Count => Reaction.Count;

	public RealDiscordReaction(DiscordReaction reaction) {
		Reaction = reaction;
	}
}
