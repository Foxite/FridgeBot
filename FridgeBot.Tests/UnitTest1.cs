using Microsoft.EntityFrameworkCore;
using Moq;

namespace FridgeBot.Tests;

public class Tests : IDisposable {
	private FridgeDbContext m_DbContext;
	private Mock<IFridgeTarget> m_MockTarget;
	private FridgeService m_FridgeService;

	[OneTimeSetUp]
	public void OneTimeSetup() {
		m_DbContext = new FridgeDbContext(
			new DbContextOptionsBuilder<FridgeDbContext>()
				.UseNpgsql(Environment.GetEnvironmentVariable("TESTDB") ?? "Host=localhost; Port=5432; Username=fridgebot; Password=test123")
				.Options
		);
		
		// Create a fake IFridgeTarget.
		// The functions don't do anything to actually deliver fridge messages.
		// However, during a test, I can assert that executing an (async) delegate results in a particular function being called on the mock object, and I can perform assertions on its parameters.
		m_MockTarget = new Moq.Mock<IFridgeTarget>(MockBehavior.Loose);

		m_FridgeService = new FridgeService(m_DbContext, m_MockTarget.Object);
	}

	[SetUp]
	public void Setup() {
		m_DbContext.Database.EnsureDeleted();
		m_DbContext.Database.EnsureCreated();

		m_DbContext.Servers.Add(
			new ServerFridge(2, new DateTimeOffset(2023, 02, 22, 12, 00, 00, TimeSpan.Zero))
				.AddEmote(new ServerEmote("hi!", 2, 0))
				.AddEmote(new ServerEmote("hey!", 2, 1))
				.AddEmote(new ServerEmote("hello!", 1, 1))
		);
		
		m_MockTarget.Invocations.Clear();
	}

	[Test]
	public void NotInGuildTest() {
		var mockMessage = new Mock<IDiscordMessage>(MockBehavior.Strict);
		mockMessage.SetupGet(m => m.GuildId).Returns((ulong?) null);

		Assert.DoesNotThrowAsync(async () => await m_FridgeService.ProcessReactionAsync(mockMessage.Object));
		m_MockTarget.VerifyNoOtherCalls();
	}

	[Test]
	public void AuthorIsCurrentTest() {
		var mockMessage = new Mock<IDiscordMessage>(MockBehavior.Strict);
		mockMessage.SetupGet(m => m.GuildId).Returns(123);
		mockMessage.SetupGet(m => m.AuthorIsCurrent).Returns(true);

		Assert.DoesNotThrowAsync(() => m_FridgeService.ProcessReactionAsync(mockMessage.Object));
		m_MockTarget.VerifyNoOtherCalls();
	}

	[Test]
	public void AddEmoteToOldPost() {
		var mockMessage = new Mock<IDiscordMessage>(MockBehavior.Strict);
		mockMessage.SetupGet(m => m.GuildId).Returns(123);
		mockMessage.SetupGet(m => m.AuthorIsCurrent).Returns(false);
		mockMessage.SetupGet(m => m.ChannelId).Returns(456);
		mockMessage.SetupGet(m => m.Id).Returns(789);
		mockMessage.SetupGet(m => m.CreationTimestamp).Returns(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

		Assert.DoesNotThrowAsync(() => m_FridgeService.ProcessReactionAsync(mockMessage.Object));
		m_MockTarget.VerifyNoOtherCalls();
	}

	[Test]
	public void AddUnrelatedEmoteToNewEntry() {
		var mockMessage = new Mock<IDiscordMessage>(MockBehavior.Strict);
		mockMessage.SetupGet(m => m.GuildId).Returns(123);
		mockMessage.SetupGet(m => m.AuthorIsCurrent).Returns(false);
		mockMessage.SetupGet(m => m.ChannelId).Returns(456);
		mockMessage.SetupGet(m => m.Id).Returns(789);
		mockMessage.SetupGet(m => m.CreationTimestamp).Returns(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
		mockMessage.SetupGet(m => m.Reactions).Returns(new[] { })

		Assert.DoesNotThrowAsync(() => m_FridgeService.ProcessReactionAsync(mockMessage.Object));
		m_MockTarget.VerifyNoOtherCalls();
	}

	public void Dispose() {
		m_DbContext.Dispose();
	}

	private static IReadOnlyCollection<IDiscordReaction> GetMockedReactions(params (string emoteString, int count)[] items) {
		return items.Select(item => GetMockedReaction())
	}

	private static IDiscordReaction GetMockedReaction(string emoteString, int count) {
		var mockEmoji = new Mock<IDiscordEmoji>(MockBehavior.Strict);
		mockEmoji.Setup(m => m.ToStringInvariant()).Returns(emoteString);
		
		var mockReaction = new Mock<IDiscordReaction>(MockBehavior.Strict);
		mockReaction.SetupGet(m => m.Count).Returns(count);
		mockReaction.SetupGet(m => m.Emoji).Returns(mockEmoji.Object);
		
		return mockReaction.Object;
	}
}
