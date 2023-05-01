namespace FridgeBot.Tests;

public class MockDiscordEmoji : IDiscordEmoji {
	public string Name { get; }
	
	public MockDiscordEmoji(string name) {
		Name = name;
	}
	
	public bool Equals(IDiscordEmoji? other) {
		return other is MockDiscordEmoji mde && mde.Name == Name;
	}

	public string ToStringInvariant() {
		return Name;
	}
}
