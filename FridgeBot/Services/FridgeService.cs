using Microsoft.EntityFrameworkCore;
using Revcord;
using Revcord.Entities;

namespace FridgeBot;

public class FridgeService {
	private readonly FridgeDbContext m_DbContext;
	private readonly IFridgeTarget m_Target;

	public FridgeService(FridgeDbContext dbContext, IFridgeTarget target) {
		m_DbContext = dbContext;
		m_Target = target;
	}

	public async Task ProcessReactionAsync(IMessage message) {
		if (!message.GuildId.HasValue) {
			return;
		}
		
		if (message.AuthorIsSelf) {
			return;
		}

		ServerFridge? fridgeServer = await m_DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == message.GuildId.Value);
		if (fridgeServer == null) {
			return;
		}

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
		         let entryEmote = new FridgeEntryEmote {EmoteString = reaction.Emoji.ToString()}
		         select entryEmote) {
			fridgeEntry.Emotes.Add(addEmote);
		}

		if (newEntry && fridgeEntry.Emotes.Count == 0) {
			return;
		}

		if (fridgeEntry.Emotes.Count == 0) {
			await m_Target.DeleteFridgeMessageAsync(fridgeEntry, message.Client);
			//await message.Client.DeleteMessageAsync(fridgeServer.ChannelId, fridgeEntry.FridgeMessageId);
			m_DbContext.Entries.Remove(fridgeEntry);
		} else if (newEntry) {
			fridgeEntry.FridgeMessageId = await m_Target.CreateFridgeMessageAsync(fridgeEntry, message);
			//IMessage fridgeMessage = await message.Client.SendMessageAsync(fridgeServer.ChannelId, new RenderableFridgeMessage(fridgeEntry, message));
			//fridgeEntry.FridgeMessageId = fridgeMessage.Id;
			m_DbContext.Entries.Add(fridgeEntry);
		} else {
			try {
				await m_Target.UpdateFridgeMessageAsync(fridgeEntry, message);
				//await message.Client.UpdateMessageAsync(fridgeServer.ChannelId, fridgeEntry.MessageId, new RenderableFridgeMessage(fridgeEntry, message));
			} catch (EntityNotFoundException) {
				m_DbContext.Entries.Remove(fridgeEntry);
			}
		}

		await m_DbContext.SaveChangesAsync();
	}
}