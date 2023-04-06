using Microsoft.EntityFrameworkCore;

namespace FridgeBot;

public class FridgeService {
	private readonly FridgeDbContext m_DbContext;
	private readonly IFridgeTarget m_FridgeTarget;
	private readonly FridgeEntryUpdaterProvider m_EntryUpdaterProvider;

	public FridgeService(FridgeDbContext dbContext, IFridgeTarget fridgeTarget, FridgeEntryUpdaterProvider entryUpdaterProvider) {
		m_DbContext = dbContext;
		m_FridgeTarget = fridgeTarget;
		m_EntryUpdaterProvider = entryUpdaterProvider;
	}

	public async Task ProcessReactionAsync(IDiscordMessage message) {
		if (!message.GuildId.HasValue) {
			return;
		}
		
		if (message.AuthorIsCurrent) {
			return;
		}

		ServerFridge? fridgeServer = await m_DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == message.GuildId);
		if (fridgeServer != null) {
			FridgeEntry? fridgeEntry = await m_DbContext.Entries.Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.ServerId == fridgeServer.Id && entry.MessageId == message.Id);
			bool newEntry = false;
			if (fridgeEntry == null) {
				newEntry = true;
				fridgeEntry = new FridgeEntry() {
					ServerId = fridgeServer.Id,
					Server = fridgeServer,
					ChannelId = message.ChannelId,
					MessageId = message.Id,
					Emotes = new List<FridgeEntryEmote>()
				};
			} else {
				fridgeEntry.ServerId = fridgeServer.Id;
				fridgeEntry.Server = fridgeServer;
			}
			
			// Update FridgeEntry
			IFridgeEntryUpdater entryUpdater = m_EntryUpdaterProvider.GetEntryUpdater(fridgeServer);
			await entryUpdater.UpdateFridgeEntry(fridgeEntry, message);

			// Update fridge message, if necessary
			if (!(newEntry && fridgeEntry.Emotes.Count == 0)) {
				if (fridgeEntry.Emotes.Count == 0) {
					await m_FridgeTarget.DeleteFridgeMessageAsync(fridgeEntry);
					m_DbContext.Entries.Remove(fridgeEntry);
				} else if (fridgeEntry.FridgeMessageId == 0) {
					fridgeEntry.FridgeMessageId = await m_FridgeTarget.CreateFridgeMessageAsync(fridgeEntry, message);
					m_DbContext.Entries.Add(fridgeEntry);
				} else {
					try {
						await m_FridgeTarget.UpdateFridgeMessageAsync(fridgeEntry, message);
					} catch (FileNotFoundException) {
						m_DbContext.Entries.Remove(fridgeEntry);
					}
				}
			}
			
			await m_DbContext.SaveChangesAsync();
		}
	}
}
