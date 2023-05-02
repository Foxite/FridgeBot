using Revcord;
using Revcord.Entities;

namespace FridgeBot;

/// <summary>
/// Used by <see cref="FridgeService"/> to delegate the actual sending of fridge messages, so we can mock that behavior.
/// </summary>
public interface IFridgeTarget {
	/// <returns>The message ID</returns>
	Task<EntityId> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message);
	
	/// <exception cref="EntityNotFoundException">If the fridge message has been removed externally</exception>
	Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message);
	
	Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry, ChatClient client);
}

public class ProductionFridgeTarget : IFridgeTarget {
	public async Task<EntityId> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) => (await message.Client.SendMessageAsync(fridgeEntry.Server.ChannelId, new RenderableFridgeMessage(fridgeEntry, message))).Id;
	public async Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) => await message.Client.UpdateMessageAsync(fridgeEntry.Server.ChannelId, fridgeEntry.FridgeMessageId, new RenderableFridgeMessage(fridgeEntry, message));
	public async Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry, ChatClient client) => await client.DeleteMessageAsync(fridgeEntry.Server.ChannelId, fridgeEntry.FridgeMessageId);
}
