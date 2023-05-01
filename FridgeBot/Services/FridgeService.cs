using Microsoft.EntityFrameworkCore;

namespace FridgeBot;

public class FridgeService {
	private readonly FridgeDbContext m_DbContext;
	private readonly IFridgeTarget m_FridgeTarget;

	public FridgeService(FridgeDbContext dbContext, IFridgeTarget fridgeTarget) {
		m_DbContext = dbContext;
		m_FridgeTarget = fridgeTarget;
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
			}
			
			// Update FridgeEntry
			foreach (FridgeEntryEmote removeEmote in
			         from entryEmote in fridgeEntry.Emotes
			         let reaction = message.Reactions.FirstOrDefault(reaction => reaction.Emoji.ToStringInvariant() == entryEmote.EmoteString)
			         where reaction == null || reaction.Count < fridgeServer.Emotes.First(serverEmote => serverEmote.EmoteString == entryEmote.EmoteString).MaximumToRemove
			         let item = fridgeEntry.Emotes.FirstOrDefault(emote => emote.EmoteString == entryEmote.EmoteString)
			         where item != null
			         select item) {
				fridgeEntry.Emotes.Remove(removeEmote);
			}
			
			foreach (FridgeEntryEmote addEmote in
			         from reaction in message.Reactions
			         let serverEmote = fridgeServer.Emotes.FirstOrDefault(emote => emote.EmoteString == reaction.Emoji.ToStringInvariant())
			         where serverEmote != null && reaction.Count >= serverEmote.MinimumToAdd && !fridgeEntry.Emotes.Any(entryEmote => entryEmote.EmoteString == serverEmote.EmoteString)
			         let entryEmote = new FridgeEntryEmote { EmoteString = reaction.Emoji.ToStringInvariant() }
			         select entryEmote) {
				fridgeEntry.Emotes.Add(addEmote);
			}

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
