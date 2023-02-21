using DSharpPlus.Entities;

namespace FridgeBot;

public interface IFridgeTarget {
	/// <returns>The message ID</returns>
	Task<ulong> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, DiscordMessage message);
	
	/// <exception cref="FileNotFoundException">If the fridge message has been removed externally</exception>
	Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, DiscordMessage message);
	
	Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry);
}
