using System.Diagnostics;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot;

public class FridgeService {
	private readonly FridgeDbContext m_DbContext;
	private readonly IFridgeTarget m_FridgeTarget;

	public FridgeService(FridgeDbContext dbContext, IFridgeTarget fridgeTarget) {
		m_DbContext = dbContext;
		m_FridgeTarget = fridgeTarget;
	}

	public async Task ProcessReactionAsync(DiscordMessage message, DiscordEmoji emoji, bool added) {
		if (message.Author.IsCurrent) {
			return;
		}

		ServerEmote? serverEmote = await m_DbContext.Emotes.Include(emote => emote.Server).Where(emote => emote.ServerId == message.Channel.GuildId).FirstOrDefaultAsync(emote => emote.EmoteString == emoji.ToStringInvariant());
		if (serverEmote != null && message.CreationTimestamp >= serverEmote.Server.InitializedAt) {
			FridgeEntry? fridgeEntry = await m_DbContext.Entries.Include(entry => entry.Server).Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.MessageId == message.Id && entry.ServerId == message.Channel.GuildId);
			FridgeEntryEmote? entryEmote = fridgeEntry?.Emotes.FirstOrDefault(fee => fee.EmoteString == emoji.ToStringInvariant());
			DiscordReaction? messageReaction = message.Reactions.FirstOrDefault(reaction => reaction.Emoji == emoji);

			if (added) {
				Debug.Assert(messageReaction != null);
				if (entryEmote == null && messageReaction.Count >= serverEmote.MinimumToAdd) {
					entryEmote = new FridgeEntryEmote() {
						EmoteString = emoji.ToStringInvariant()
					};

					fridgeEntry ??= new FridgeEntry() {
						ChannelId = message.Channel.Id,
						MessageId = message.Id,
						ServerId = message.Channel.Guild.Id,
						Emotes = new List<FridgeEntryEmote>(),
						Server = serverEmote.Server,
					};

					fridgeEntry.Emotes.Add(entryEmote);
				}
			} else {
				if (entryEmote != null && (messageReaction == null || messageReaction.Count <= serverEmote.MaximumToRemove)) {
					Debug.Assert(fridgeEntry != null);
					fridgeEntry.Emotes.Remove(entryEmote);
				}
			}

			if (fridgeEntry != null) {
				if (fridgeEntry.Emotes.Count == 0) {
					await m_FridgeTarget.DeleteFridgeMessageAsync(fridgeEntry);
					m_DbContext.Entries.Remove(fridgeEntry);
				} else if (fridgeEntry.FridgeMessageId == 0) {
					fridgeEntry.FridgeMessageId = await m_FridgeTarget.CreateFridgeMessageAsync(fridgeEntry, message);
					m_DbContext.Entries.Add(fridgeEntry);
				} else {
					try {
						await m_FridgeTarget.UpdateFridgeMessageAsync(fridgeEntry, message);
					} catch (NotFoundException) {
						m_DbContext.Entries.Remove(fridgeEntry);
					}
				}
			}
			
			await m_DbContext.SaveChangesAsync();
		}
	}
}
