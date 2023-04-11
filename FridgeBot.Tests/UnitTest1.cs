using CorporateEspionage;
using CorporateEspionage.NUnit;
using Microsoft.EntityFrameworkCore;
using Revcord;
using Revcord.Entities;

namespace FridgeBot.Tests;

public class Tests {
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
			await m_DbContext.DisposeAsync();
			m_DbContext = null;
		}

		m_DbContext = new FridgeDbContext(
			new DbContextOptionsBuilder<FridgeDbContext>()
				.UseSqlite("DataSource=:memory:")
				.Options
		);

		await m_DbContext.Database.OpenConnectionAsync(); // must be done for some reason
		await m_DbContext.Database.EnsureCreatedAsync();

		m_ServerFridge = new ServerFridge(EntityId.Of(123), EntityId.Of(2), new DateTimeOffset(2023, 02, 22, 12, 00, 00, TimeSpan.Zero))
			.AddEmote(new ServerEmote("hi!", 2, 0))
			.AddEmote(new ServerEmote("hey!", 2, 1))
			.AddEmote(new ServerEmote("hello!", 1, 1));
		
		m_DbContext.Servers.Add(m_ServerFridge);

		await m_DbContext.SaveChangesAsync();
		
		// Create a fake IFridgeTarget.
		// The functions don't do anything to actually deliver fridge messages.
		// However, during a test, I can assert that executing an (async) delegate results in a particular function being called on the mock object, and I can perform assertions on its parameters.
		m_MockTarget = m_SpyGenerator.CreateSpy<IFridgeTarget>();
		m_FridgeService = new FridgeService(m_DbContext, m_MockTarget.Object);
	}
	
	[OneTimeTearDown]
	public void Teardown() {
		m_DbContext?.Dispose();
	}

	[Test]
	public async Task NotInGuild() {
		var mockMessage = new MockMessage(false, null, 456, 789, DateTimeOffset.MaxValue, reactions: new MockReaction(new MockEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task AuthorIsCurrent() {
		var mockMessage = new MockMessage(true, 123, 456, 789, DateTimeOffset.MaxValue, reactions: new MockReaction(new MockEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task SufficientReactionsOnOldPost() {
		var mockMessage = new MockMessage(false, 123, 456, 789, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockReaction(new MockEmoji("hello!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task UnrelatedReactionsOnNewEntry() {
		var mockMessage = new MockMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockReaction(new MockEmoji("eyoo!"), 2));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task InsufficientReactionNewEntry() {
		var mockMessage = new MockMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockReaction(new MockEmoji("hey!"), 1));
		await m_FridgeService.ProcessReactionAsync(mockMessage);

		Assert.Multiple(() => {
			Assert.That(m_MockTarget, Was.NoOtherCalls());
		});
	}

	[Test]
	public async Task SufficientReactionToNewEntry() {
		// TODO need to add return value configuration to CorporateEspionage for this test case to work
		// Previously, it returned the default ulong which is fine for EntityFramework
		// Now it returns a default EntityId which is not fine as UnderlyingId is null
		var mockMessage = new MockMessage(false, m_ServerFridge.Id, EntityId.Of(456), EntityId.Of(789), new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockReaction(new MockEmoji("hello!"), 2));
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
		var mockMessage = new MockMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), reactions: new MockReaction(new MockEmoji("hello!"), 3));

		EntityId fridgeMessageId = EntityId.Of(234);
		{
			var fridgeEntry = new FridgeEntry() {
				ChannelId = mockMessage.ChannelId,
				MessageId = mockMessage.Id,
				FridgeMessageId = fridgeMessageId,
				ServerId = m_ServerFridge.Id,
				Emotes = new HashSet<FridgeEntryEmote>() {
					new FridgeEntryEmote() {
						EmoteString = new MockEmoji("hello!").ToString()
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
		var mockMessage = new MockMessage(false, 123, 456, 789, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

		EntityId fridgeMessageId = EntityId.Of(234);
		{
			var fridgeEntry = new FridgeEntry() {
				ChannelId = mockMessage.ChannelId,
				MessageId = mockMessage.Id,
				FridgeMessageId = fridgeMessageId,
				ServerId = m_ServerFridge.Id,
				Emotes = new HashSet<FridgeEntryEmote>() {
					new FridgeEntryEmote() {
						EmoteString = new MockEmoji("hello!").ToString()
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
}

public class MockMessage : IMessage {
	private readonly bool m_IsSelf;
	
	public IUser Author => new MockUser(m_IsSelf);
	public EntityId Id { get; }
	public EntityId? GuildId { get; }
	public EntityId ChannelId { get; }
	public IReadOnlyCollection<IReaction> Reactions { get; }
	public DateTimeOffset CreationTimestamp { get; }

	public string? Content => throw new NotImplementedException();
	public IGuild? Guild => throw new NotImplementedException();
	public IGuildMember? AuthorMember => throw new NotImplementedException();
	public IChannel Channel => throw new NotImplementedException();
	public string JumpLink => throw new NotImplementedException();
	
	public EntityId AuthorId => throw new NotImplementedException();
	public bool IsSystemMessage => throw new NotImplementedException();
	public ChatClient Client => throw new NotImplementedException();

	public MockMessage(bool isSelf, int? guildId, int channelId, int id, DateTimeOffset creationTimestamp, params IReaction[] reactions)
		: this(isSelf, guildId.HasValue ? EntityId.Of(guildId.Value) : null, EntityId.Of(channelId), EntityId.Of(id), creationTimestamp, reactions) { }

	public MockMessage(bool isSelf, EntityId? guildId, EntityId channelId, EntityId id, DateTimeOffset creationTimestamp, params IReaction[] reactions) {
		GuildId = guildId;
		ChannelId = channelId;
		Id = id;
		CreationTimestamp = creationTimestamp;
		Reactions = reactions;
		m_IsSelf = isSelf;
	}
}

public class MockUser : IUser {
	public bool IsSelf { get; }

	public ChatClient Client => throw new NotImplementedException();
	public EntityId Id => throw new NotImplementedException();
	public string DisplayName => throw new NotImplementedException();
	public string Username => throw new NotImplementedException();
	public string DiscriminatedUsername => throw new NotImplementedException();
	public string MentionString => throw new NotImplementedException();
	public string AvatarUrl => throw new NotImplementedException();
	public bool IsBot => throw new NotImplementedException();

	public MockUser(bool isSelf) {
		IsSelf = isSelf;
	}
}

public class MockReaction : IReaction {
	public ChatClient Client => throw new NotImplementedException();
	public IEmoji Emoji { get; }
	public int Count { get; }

	public MockReaction(MockEmoji emoji, int count) {
		Emoji = emoji;
		Count = count;
	}
}

public class MockEmoji : IEmoji {
	public string Name { get; }
	
	public ChatClient Client => throw new NotImplementedException();
	public EntityId Id => throw new NotImplementedException();
	public bool IsAnimated => throw new NotImplementedException();
	public bool IsCustomizedEmote => throw new NotImplementedException();

	public MockEmoji(string name) {
		Name = name;
	}

	public bool Equals(IEmoji? other) {
		return other is MockEmoji me && me.Name == Name;
	}

	public override string ToString() {
		return Name;
	}
}
