using Microsoft.EntityFrameworkCore;
using Revcord.Entities;

namespace FridgeBot;

public class FridgeService {
	private readonly FridgeDbContext m_DbContext;
	private readonly IEnumerable<IFridgeTarget> m_FridgeTargets;

	public FridgeService(FridgeDbContext dbContext, IEnumerable<IFridgeTarget> fridgeTargets) {
		m_DbContext = dbContext;
		m_FridgeTargets = fridgeTargets;
	}

	public async Task ProcessReactionAsync(IMessage message) {
		if (!message.GuildId.HasValue) {
			return;
		}
		
		if (message.AuthorIsSelf) {
			return;
		}

		IFridgeTarget? target = m_FridgeTargets.FirstOrDefault(target => target.Supports(message.Client));

		if (target == null) {
			return;
		}

		ServerFridge? fridgeServer = await m_DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == message.GuildId.Value);
		if (fridgeServer != null) {
			if (message.CreationTimestamp < fridgeServer.InitializedAt) {
				return;
			}
			
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
			         let reaction = message.Reactions.FirstOrDefault(reaction => reaction.Emoji.ToString() == entryEmote.EmoteString)
			         where reaction == null || reaction.Count < fridgeServer.Emotes.First(serverEmote => serverEmote.EmoteString == entryEmote.EmoteString).MaximumToRemove
			         let item = fridgeEntry.Emotes.FirstOrDefault(emote => emote.EmoteString == entryEmote.EmoteString)
			         where item != null
			         select item) {
				fridgeEntry.Emotes.Remove(removeEmote);
			}
			
			foreach (FridgeEntryEmote addEmote in
			         from reaction in message.Reactions
			         let serverEmote = fridgeServer.Emotes.FirstOrDefault(emote => emote.EmoteString == reaction.Emoji.ToString())
			         where serverEmote != null && reaction.Count >= serverEmote.MinimumToAdd && !fridgeEntry.Emotes.Any(entryEmote => entryEmote.EmoteString == serverEmote.EmoteString)
			         let entryEmote = new FridgeEntryEmote { EmoteString = reaction.Emoji.ToString() }
			         select entryEmote) {
				fridgeEntry.Emotes.Add(addEmote);
			}

			// Update fridge message, if necessary
			if (!(newEntry && fridgeEntry.Emotes.Count == 0)) {
				if (fridgeEntry.Emotes.Count == 0) {
					await target.DeleteFridgeMessageAsync(fridgeEntry, message.Client);
					m_DbContext.Entries.Remove(fridgeEntry);
				} else if (newEntry) {
					fridgeEntry.FridgeMessageId = await target.CreateFridgeMessageAsync(fridgeEntry, message);
					m_DbContext.Entries.Add(fridgeEntry);
				} else {
					try {
						await target.UpdateFridgeMessageAsync(fridgeEntry, message);
					} catch (FileNotFoundException) {
						m_DbContext.Entries.Remove(fridgeEntry);
					}
				}
			}
			
			await m_DbContext.SaveChangesAsync();
		}
	}
}
