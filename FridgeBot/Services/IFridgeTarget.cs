using Revcord;
using Revcord.Entities;

namespace FridgeBot;

public interface IFridgeTarget {
	/// <returns>The message ID</returns>
	Task<EntityId> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message);
	
	/// <exception cref="FileNotFoundException">If the fridge message has been removed externally</exception>
	Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message);
	
	Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry, ChatClient client);
}
