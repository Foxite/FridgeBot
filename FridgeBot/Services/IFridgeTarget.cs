using DSharpPlus.Entities;

namespace FridgeBot;

public interface IFridgeTarget {
	/// <returns>The message ID</returns>
	Task<ulong> CreateFridgeMessageAsync(DiscordMessage message, FridgeEntry fridgeEntry);
	
	/// <exception cref="FileNotFoundException">If the fridge message has been removed externally</exception>
	Task UpdateFridgeMessageAsync(DiscordMessage message, FridgeEntry fridgeEntry);
	
	Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry);
}
