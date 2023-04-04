namespace FridgeBot.Tests;

public class MockDiscordMessage : IDiscordMessage {
	public bool AuthorIsCurrent { get; }
	public ulong? GuildId { get; }
	public ulong ChannelId { get; }
	public ulong Id { get; }
	public DateTimeOffset CreationTimestamp { get; }
	public IReadOnlyCollection<MockDiscordReaction> Reactions { get; }
	IReadOnlyCollection<IDiscordReaction> IDiscordMessage.Reactions => Reactions;

	public MockDiscordMessage(bool authorIsCurrent = false, ulong? guildId = null, ulong channelId = 1, ulong id = 2, DateTimeOffset creationTimestamp = default, params MockDiscordReaction[] reactions) {
		AuthorIsCurrent = authorIsCurrent;
		GuildId = guildId;
		ChannelId = channelId;
		Id = id;
		CreationTimestamp = creationTimestamp;
		Reactions = reactions;
	}
}
