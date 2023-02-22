using DSharpPlus.Entities;

namespace FridgeBot;

public interface IFridgeTarget {
	/// <returns>The message ID</returns>
	Task<ulong> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IDiscordMessage message);
	
	/// <exception cref="FileNotFoundException">If the fridge message has been removed externally</exception>
	Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IDiscordMessage message);
	
	Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry);
}
