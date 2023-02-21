using System.Diagnostics;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FridgeBot;

public class FridgeService {
	private readonly IDbContextFactory<FridgeDbContext> m_DbContextFactory;
	private readonly ILogger<Program> m_Logger;
	private readonly NotificationService m_Notifications;
	private readonly IFridgeTarget m_FridgeTarget;

	public FridgeService(IDbContextFactory<FridgeDbContext> dbContextFactory, ILogger<Program> logger, NotificationService notifications, IFridgeTarget fridgeTarget) {
		m_DbContextFactory = dbContextFactory;
		m_Notifications = notifications;
		m_FridgeTarget = fridgeTarget;
		m_Logger = logger;
	}

	public async Task ProcessReactionAsync(DiscordMessage message, DiscordEmoji emoji, bool added) {
		FridgeDbContext? dbcontext = null;
		try {
			if (message.Author.IsCurrent) {
				return;
			}

			dbcontext = await m_DbContextFactory.CreateDbContextAsync();
			ServerEmote? serverEmote = await dbcontext.Emotes.Include(emote => emote.Server).Where(emote => emote.ServerId == message.Channel.GuildId).FirstOrDefaultAsync(emote => emote.EmoteString == emoji.ToStringInvariant());
			if (serverEmote != null && message.CreationTimestamp >= serverEmote.Server.InitializedAt) {
				FridgeEntry? fridgeEntry = await dbcontext.Entries.Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.MessageId == message.Id && entry.ServerId == message.Channel.GuildId);
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
							Emotes = new List<FridgeEntryEmote>()
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
					// TODO find a way to skip intermediate discord api calls and send/update/delete the message directly
					if (fridgeEntry.Emotes.Count == 0) {
						await m_FridgeTarget.DeleteFridgeMessageAsync(fridgeEntry);
						dbcontext.Entries.Remove(fridgeEntry);
					} else if (fridgeEntry.FridgeMessageId == 0) {
						fridgeEntry.FridgeMessageId = await m_FridgeTarget.CreateFridgeMessageAsync(fridgeEntry, message);
						dbcontext.Entries.Add(fridgeEntry);
					} else {
						try {
							await m_FridgeTarget.UpdateFridgeMessageAsync(fridgeEntry, message);
						} catch (NotFoundException) {
							dbcontext.Entries.Remove(fridgeEntry);
						}
					}
				}
			}
		} catch (Exception ex) {
			string N(object? o) => o?.ToString() ?? "null";
			FormattableString errorMessage =
				@$"Exception in OnReactionModifiedAsync
					   author: {N(message?.Author?.Id)} ({N(message?.Author?.Username)}#{N(message?.Author?.Discriminator)}), bot: {N(message?.Author?.IsBot)}
					   message: {N(message?.Id)} ({N(message?.JumpLink)}), type: {N(message?.MessageType?.ToString() ?? "(null)")}, webhook: {N(message?.WebhookMessage)}
					   channel {N(message?.Channel?.Id)} ({N(message?.Channel?.Name)})
					   {(message?.Channel?.Guild != null ? $"guild {N(message?.Channel?.Guild?.Id)} ({N(message?.Channel?.Guild?.Name)})" : "")}";
			m_Logger.LogCritical(ex, errorMessage);
			await m_Notifications.SendNotificationAsync(errorMessage, ex.Demystify());
		} finally {
			if (dbcontext != null) {
				await dbcontext.DisposeAsync();
			}
		}
	}
}
