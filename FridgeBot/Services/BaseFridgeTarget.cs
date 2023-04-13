using Foxite.Common.Notifications;
using Microsoft.Extensions.Logging;
using Revcord;
using Revcord.Entities;
using RevMessage = Revcord.Discord.DiscordMessage;
using RevChannel = Revcord.Discord.DiscordChannel;
using RevMember = Revcord.Discord.DiscordMember;
using RevEmoji = Revcord.Discord.DiscordEmoji;

using SharpMessage = DSharpPlus.Entities.DiscordMessage;
using SharpChannel = DSharpPlus.Entities.DiscordChannel;
using SharpMember = DSharpPlus.Entities.DiscordMember;
using SharpEmoji = DSharpPlus.Entities.DiscordEmoji;
using SharpReaction = DSharpPlus.Entities.DiscordReaction;

namespace FridgeBot;

public abstract class BaseFridgeTarget : IFridgeTarget {
	protected ILogger<BaseFridgeTarget> Logger { get; }
	protected NotificationService Notifications { get; }

	public BaseFridgeTarget(ILogger<BaseFridgeTarget> logger, NotificationService notifications) {
		Logger = logger;
		Notifications = notifications;
	}

	public abstract bool Supports(ChatClient client);

	public async Task<EntityId> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) {
		return await ExecuteCreateAsync(fridgeEntry, message);
	}
	
	public async Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) {
		try {
			IMessage fridgeMessage = await message.Client.GetMessageAsync(fridgeEntry.Server.ChannelId, fridgeEntry.FridgeMessageId);
			await ExecuteUpdateAsync(fridgeEntry, message, fridgeMessage);
		} catch (EntityNotFoundException ex) {
			throw new FileNotFoundException("The fridge message has been deleted externally", ex);
		}
	}
	
	public async Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry, ChatClient client) {
		try {
			IMessage fridgeMessage = await client.GetMessageAsync(fridgeEntry.Server.ChannelId, fridgeEntry.FridgeMessageId);
			await fridgeMessage.DeleteAsync();
		} catch (EntityNotFoundException) { }
	}

	protected abstract Task<EntityId> ExecuteCreateAsync(FridgeEntry entry, IMessage message);
	protected abstract Task ExecuteUpdateAsync(FridgeEntry entry, IMessage message, IMessage existingFridgeMessage);
}
