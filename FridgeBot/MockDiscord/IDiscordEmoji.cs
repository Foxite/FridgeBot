using DSharpPlus.Entities;

namespace FridgeBot;

/// <summary>
/// An interface for <see cref="DiscordEmoji"/> so that we can mock it.
/// </summary>
public interface IDiscordEmoji : IEquatable<IDiscordEmoji> {
	string ToStringInvariant();
}

public class RealDiscordEmoji : IDiscordEmoji, IEquatable<RealDiscordEmoji> {
	public DiscordEmoji Emoji { get; }

	public RealDiscordEmoji(DiscordEmoji emoji) {
		Emoji = emoji;
	}
	
	public string ToStringInvariant() {
		return Emoji.ToStringInvariant();
	}

	public bool Equals(RealDiscordEmoji? other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return Emoji.Equals(other.Emoji);
	}

	public bool Equals(IDiscordEmoji? other) {
		return Equals((object?) other);
	}

	public override bool Equals(object? obj) {
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != this.GetType()) return false;
		return Equals((RealDiscordEmoji) obj);
	}

	public override int GetHashCode() {
		return Emoji.GetHashCode();
	}

	public static bool operator ==(RealDiscordEmoji? left, RealDiscordEmoji? right) {
		return Equals(left, right);
	}

	public static bool operator !=(RealDiscordEmoji? left, RealDiscordEmoji? right) {
		return !Equals(left, right);
	}
}
