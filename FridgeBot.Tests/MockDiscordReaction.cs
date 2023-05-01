namespace FridgeBot.Tests;

public class MockDiscordReaction : IDiscordReaction {
	public int Count { get; }
	public MockDiscordEmoji Emoji { get; }
	IDiscordEmoji IDiscordReaction.Emoji => Emoji;

	public MockDiscordReaction(MockDiscordEmoji emoji, int count) {
		Emoji = emoji;
		Count = count;
	}
}
