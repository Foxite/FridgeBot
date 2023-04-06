namespace FridgeBot;

public class FridgeEntryUpdaterProvider {
	public IFridgeEntryUpdater GetEntryUpdater(ServerFridge fridgeServer) {
		// TODO switch implementation based on server settings
		return new DefaultFridgeEntryUpdater();
	}
}
