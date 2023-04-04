using CorporateEspionage;
using CorporateEspionage.NUnit;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot.Tests;

// These tests require a running database, see OneTimeSetup.
public class Tests : IDisposable {
	private FridgeDbContext? m_DbContext;
	private Spy<IFridgeTarget> m_MockTarget;
	private FridgeService m_FridgeService;
	private SpyGenerator m_SpyGenerator;
	private ServerFridge m_ServerFridge;

	[OneTimeSetUp]
	public void OneTimeSetup() {
		m_SpyGenerator = new SpyGenerator();
	}

	[SetUp]
	public async Task Setup() {
		if (m_DbContext != null) {
			await m_DbContext.Database.EnsureDeletedAsync();
			await m_DbContext.DisposeAsync();
			m_DbContext = null;
		}

		m_DbContext = new FridgeDbContext(
			new DbContextOptionsBuilder<FridgeDbContext>()
				.UseNpgsql(Environment.GetEnvironmentVariable("TESTDB") ?? "Host=localhost; Port=5432; Username=fridgebot; Password=test123")
				.Options
		);

		await m_DbContext.Database.EnsureDeletedAsync();
		await m_DbContext.Database.EnsureCreatedAsync();

		m_ServerFridge = new ServerFridge(123, 2, new DateTimeOffset(2023, 02, 22, 12, 00, 00, TimeSpan.Zero))
			.AddEmote(new ServerEmote("hi!", 2, 0))
			.AddEmote(new ServerEmote("hey!", 2, 1))
			.AddEmote(new ServerEmote("hello!", 1, 1));
		m_DbContext.Servers.Add(
			m_ServerFridge
		);

		await m_DbContext.SaveChangesAsync();
		
		// Create a fake IFridgeTarget.
		// The functions don't do anything to actually deliver fridge messages.
		// However, during a test, I can assert that executing an (async) delegate results in a particular function being called on the mock object, and I can perform assertions on its parameters.
		m_MockTarget = m_SpyGenerator.CreateSpy<IFridgeTarget>();
		m_FridgeService = new FridgeService(m_DbContext, m_MockTarget.Object);
	}

	[Test]
	public async Task NotInGuild() {
		var mockMessage = new MockDiscordMessage(guildId: null, reactions: new MockDiscordReaction(new MockDiscordEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task AuthorIsCurrent() {
		var mockMessage = new MockDiscordMessage(true, 123, reactions: new MockDiscordReaction(new MockDiscordEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task SufficientReactionsOnOldPost() {
		var mockMessage = new MockDiscordMessage(true, 123, 456, 789, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockDiscordReaction(new MockDiscordEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task UnrelatedReactionsOnNewEntry() {
		var mockMessage = new MockDiscordMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockDiscordReaction(new MockDiscordEmoji("eyoo!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task InsufficientReactionNewEntry() {
		var mockMessage = new MockDiscordMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockDiscordReaction(new MockDiscordEmoji("hey!"), 1));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task SufficientReactionToNewEntry() {
		var mockMessage = new MockDiscordMessage(false, m_ServerFridge.Id, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockDiscordReaction(new MockDiscordEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(
				m_MockTarget,
				Was
					.Called(() => m_MockTarget.Object.CreateFridgeMessageAsync(null!, null!))
					.Times(1)
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.MessageId)).EqualTo(mockMessage.Id))
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.ServerId)).EqualTo(m_ServerFridge.Id))
			);
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task SufficientReactionToExistingEntry() {
		var mockMessage = new MockDiscordMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockDiscordReaction(new MockDiscordEmoji("hello!"), 3));

		const ulong fridgeMessageId = 234;
		{
			var fridgeEntry = new FridgeEntry() {
				ChannelId = mockMessage.ChannelId,
				MessageId = mockMessage.Id,
				FridgeMessageId = fridgeMessageId,
				ServerId = m_ServerFridge.Id,
				Emotes = new HashSet<FridgeEntryEmote>() {
					new FridgeEntryEmote() {
						EmoteString = new MockDiscordEmoji("hello!").ToStringInvariant()
					}
				}
			};

			m_DbContext.Add(fridgeEntry);
		}
		
		await m_DbContext.SaveChangesAsync();
		
		await m_FridgeService.ProcessReactionAsync(mockMessage);
		Assert.Multiple(() => {
			Assert.That(
				m_MockTarget,
				Was
					.Called(() => m_MockTarget.Object.UpdateFridgeMessageAsync(null!, null!))
					.Times(1)
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.MessageId)).EqualTo(mockMessage.Id))
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.FridgeMessageId)).EqualTo(fridgeMessageId))
			);
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task InsufficientReactionsToExistingEntry() {
		var mockMessage = new MockDiscordMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

		const ulong fridgeMessageId = 234;
		{
			var fridgeEntry = new FridgeEntry() {
				ChannelId = mockMessage.ChannelId,
				MessageId = mockMessage.Id,
				FridgeMessageId = fridgeMessageId,
				ServerId = m_ServerFridge.Id,
				Emotes = new HashSet<FridgeEntryEmote>() {
					new FridgeEntryEmote() {
						EmoteString = new MockDiscordEmoji("hello!").ToStringInvariant()
					}
				}
			};

			m_DbContext.Add(fridgeEntry);
		}
		
		await m_DbContext.SaveChangesAsync();
		
		await m_FridgeService.ProcessReactionAsync(mockMessage);
		Assert.Multiple(() => {
			Assert.That(
				m_MockTarget,
				Was
					.Called(() => m_MockTarget.Object.DeleteFridgeMessageAsync(null!))
					.Times(1)
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.MessageId)).EqualTo(mockMessage.Id))
					.With(0, "fridgeEntry", Has.Property(nameof(FridgeEntry.FridgeMessageId)).EqualTo(fridgeMessageId))
			);
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	public void Dispose() {
		m_DbContext?.Dispose();
	}
}
