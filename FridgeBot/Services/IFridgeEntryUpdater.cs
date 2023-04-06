namespace FridgeBot;

// TODO implementation that identifies identical emotes
public interface IFridgeEntryUpdater {
	Task UpdateFridgeEntry(FridgeEntry entry, IDiscordMessage message);
}
