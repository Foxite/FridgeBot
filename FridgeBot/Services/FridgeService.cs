using System.Diagnostics;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FridgeBot;

public class FridgeService {
		
	private static async Task OnReactionModifiedAsync(DiscordClient discordClient, DiscordMessage message, DiscordEmoji emoji, bool added) {
		FridgeDbContext? dbcontext = null;
		try {
			// Acquire additional data such as the author, and refresh reaction counts
			try {
				message = await message.Channel.GetMessageAsync(message.Id);
			} catch (NotFoundException) {
				// Message was deleted since the event fired
				return;
			}
			if (message == null) {
				// Also deleted? idk. better safe than sorry
				return;
			}

			if (message.Author.IsCurrent) {
				return;
			}

			dbcontext = Host.Services.GetRequiredService<FridgeDbContext>();
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
						dbcontext.Entries.Remove(fridgeEntry);
						DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
						DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
						await fridgeMessage.DeleteAsync();
					} else if (fridgeEntry.FridgeMessageId == 0) {
						DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
						DiscordMessage fridgeMessage = await fridgeChannel.SendMessageAsync(GetFridgeMessageBuilder(message, fridgeEntry, null));
						fridgeEntry.FridgeMessageId = fridgeMessage.Id;
						dbcontext.Entries.Add(fridgeEntry);
					} else {
						try {
							DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
							DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
							await fridgeMessage.ModifyAsync(GetFridgeMessageBuilder(message, fridgeEntry, fridgeMessage));
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
			Host.Services.GetRequiredService<ILogger<Program>>().LogCritical(ex, errorMessage);
			await Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync(errorMessage, ex.Demystify());
		} finally {
			if (dbcontext != null) {
				await dbcontext.SaveChangesAsync();
				await dbcontext.DisposeAsync();
			}
		}
	}
}
