using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common.Notifications;
using Microsoft.Extensions.Logging;

namespace FridgeBot;

public class DiscordFridgeTarget : IFridgeTarget {
	private readonly ILogger<DiscordFridgeTarget> m_Logger;
	private readonly NotificationService m_Notifications;
	private readonly HttpClient m_Http;
	private readonly DiscordClient m_DiscordClient;

	public DiscordFridgeTarget(ILogger<DiscordFridgeTarget> logger, NotificationService notifications, HttpClient http, DiscordClient discordClient) {
		m_Logger = logger;
		m_Notifications = notifications;
		m_Http = http;
		m_DiscordClient = discordClient;
	}
	
	public async Task<ulong> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IDiscordMessage message) {
		// TODO find a way to skip intermediate discord api calls and send/update/delete the message directly
		DiscordChannel fridgeChannel = await m_DiscordClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		DiscordMessage fridgeMessage = await fridgeChannel.SendMessageAsync(GetFridgeMessageBuilder(((RealDiscordMessage) message).Message, fridgeEntry, null));
		return fridgeMessage.Id;
	}
	
	public async Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IDiscordMessage message) {
		DiscordChannel fridgeChannel = await m_DiscordClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		try {
			DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
			await fridgeMessage.ModifyAsync(GetFridgeMessageBuilder(((RealDiscordMessage) message).Message, fridgeEntry, fridgeMessage));
		} catch (NotFoundException ex) {
			throw new FileNotFoundException("The fridge message has been deleted externally", ex);
		}
	}
	
	public async Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry) {
		DiscordChannel fridgeChannel = await m_DiscordClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		try {
			DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
			await fridgeMessage.DeleteAsync();
		} catch (NotFoundException) { }
	}
	
	private Action<DiscordMessageBuilder> GetFridgeMessageBuilder(DiscordMessage message, FridgeEntry fridgeEntry, DiscordMessage? existingFridgeMessage) {
		return dmb => {
			string? replyingToNickname = null;
			if (message.ReferencedMessage != null) {
				if (message.ReferencedMessage.Author is DiscordMember replyingToMember && !string.IsNullOrEmpty(replyingToMember.Nickname)) {
					replyingToNickname = replyingToMember.Nickname;
				} else {
					replyingToNickname = message.ReferencedMessage.Author.Username;
				}
			}
			
			var reactions = new Dictionary<DiscordEmoji, int>();
			foreach (DiscordReaction reaction in message.Reactions) {
				if (fridgeEntry.Emotes.Any(emote => emote.EmoteString == reaction.Emoji.ToStringInvariant())) {
					reactions[reaction.Emoji] = reaction.Count;
				}
			}
			
			var content = new StringBuilder();
			int i = 0;
			foreach ((DiscordEmoji? emoji, _) in reactions) {
				if (i > 0) {
					content.Append(i == reactions.Count - 1 ? " & " : ", ");
				}
				content.Append(emoji.ToString()); // String should not be normalized here because it gets sent to discord, rather than just stored in the database.
				i++;
			}

			content.AppendLine(" moment in " + message.Channel.Mention + "!");
			
			foreach ((DiscordEmoji? emoji, int count) in reactions) {
				content.AppendLine($"{emoji.ToString()} x{count}"); // See above
			}
			
			dmb.Content = content.ToString();

			string authorName;
			string authorAvatarUrl;
			if (message.MessageType == MessageType.AutoModAlert) {
				authorName = "AutoMod";
				authorAvatarUrl = "https://discord.com/assets/b3e8bfa5e3780afd7a4f9a1695776e16.png";
			} else if (message.Author is DiscordMember authorMember) {
				authorName = authorMember.Nickname ?? authorMember.Username;
				authorAvatarUrl = authorMember.GuildAvatarUrl ?? authorMember.AvatarUrl;
			} else {
				authorName = message.Author.Username;
				authorAvatarUrl = message.Author.AvatarUrl;
			}

			string description = message.MessageType switch {
				MessageType.Default or MessageType.Reply => message.Content, // No need to check the length because the max length of a discord message is 4000 with nitro, but the max length of an embed description is 4096.
				MessageType.ChannelPinnedMessage => $"{authorName} pinned a message to the channel.",
				MessageType.ApplicationCommand => $"{(message.Interaction.User is DiscordMember interactionMember ? interactionMember.Nickname : message.Interaction.User.Username)} used ${message.Interaction.Name}",
				MessageType.GuildMemberJoin => $"{authorName} has joined the server!",
				MessageType.UserPremiumGuildSubscription => $"{authorName} has just boosted the server!",
				MessageType.TierOneUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 1**!",
				MessageType.TierTwoUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 2**!",
				MessageType.TierThreeUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 3**!",
				MessageType.RecipientAdd => $"{authorName} joined the thread!", // Does not actually seem to happen
				MessageType.RecipientRemove => $"{authorName} removed {(message.MentionedUsers[0] as DiscordMember)?.Nickname ?? message.MentionedUsers[0].Username} from the thread.",
				MessageType.AutoModAlert => $"AutoMod has blocked a message from {(message.Author as DiscordMember)?.DisplayName ?? message.Author.Username}",
				MessageType.ChannelFollowAdd => $"{authorName} has added {message.Content} to this channel. Its most important updates will show up here.",
				MessageType.Call => $"{authorName} has started a call.",
				/*
				MessageType.ChannelNameChange => "ChannelNameChange", // should not happen in guilds
				MessageType.ChannelIconChange => "ChannelIconChange", // should not happen in guilds
				MessageType.GuildDiscoveryDisqualified => "GuildDiscoveryDisqualified",
				MessageType.GuildDiscoveryRequalified => "GuildDiscoveryRequalified",
				MessageType.GuildDiscoveryGracePeriodInitialWarning => "GuildDiscoveryGracePeriodInitialWarning",
				MessageType.GuildDiscoveryGracePeriodFinalWarning => "GuildDiscoveryGracePeriodFinalWarning",
				MessageType.GuildInviteReminder => "GuildInviteReminder",
				MessageType.ContextMenuCommand => "ContextMenuCommand",
				*/
				_ => $"Unknown message type: {message.MessageType.ToString()}"
			};

			string? imageUrl = null;
			if (message.Attachments.Count > 0) {
				imageUrl = message.Attachments.FirstOrDefault(att => att.MediaType != null && att.MediaType.StartsWith("image/"))?.Url;
				if (imageUrl == null) {
					DiscordAttachment? videoAttachment = message.Attachments.FirstOrDefault(att => att.MediaType != null && att.MediaType.StartsWith("video/"));
					if (videoAttachment != null && (existingFridgeMessage == null || existingFridgeMessage.Attachments.Count == 0)) {
						try {
							using Stream download = m_Http.GetStreamAsync(videoAttachment.Url).Result;
							var memory = new MemoryStream();
							download.CopyTo(memory);
							memory.Position = 0;
							dmb.WithFile(videoAttachment.FileName, memory);
						} catch (Exception e) {
							m_Logger.LogError(e, "Error downloading attachment {videoUrl}", videoAttachment.Url);
							m_Notifications.SendNotificationAsync($"Error downloading attachment {videoAttachment.Url}, ignoring", e);
						}
					}
				}
			}

			var embedBuilder = new DiscordEmbedBuilder() {
				Author = new DiscordEmbedBuilder.EmbedAuthor() {
					Name = authorName,
					IconUrl = authorAvatarUrl
				},
				Color = new Optional<DiscordColor>(DiscordColor.Azure),
				Description = description,
				Footer = new DiscordEmbedBuilder.EmbedFooter() {
					Text = message.Id.ToString()
				},
				ImageUrl = imageUrl,
				Timestamp = message.Timestamp,
				Url = message.JumpLink.ToString()
			};
			
			embedBuilder.AddField("Jump to message", Formatter.MaskedUrl("Click here to jump", message.JumpLink));

			if (message.ReferencedMessage != null) {
				string fieldName = "Replying to " + replyingToNickname;
				if (fieldName.Length > 255) {
					fieldName = fieldName[..255];
				}
				embedBuilder.AddField(fieldName, Formatter.MaskedUrl("Click here to jump", message.ReferencedMessage.JumpLink));
			}
			
			DiscordEmbed? copyEmbed = null;

			if (message.Embeds.Count > 0 && message.Embeds[0].Type != "auto_moderation_message") {
				DiscordEmbed sourceEmbed = message.Embeds[0];
				var copyEmbedBuilder = new DiscordEmbedBuilder(sourceEmbed);
				// Empty embeds may occur when embedding a media link (video/image or something)
				// Do not copy empty embeds.
				if (copyEmbedBuilder.Author != null || !string.IsNullOrEmpty(copyEmbedBuilder.Title) || !string.IsNullOrEmpty(copyEmbedBuilder.Description) || copyEmbedBuilder.Fields.Count > 0) {
					copyEmbed = copyEmbedBuilder.Build();
				} else if (embedBuilder.ImageUrl == null && copyEmbedBuilder.ImageUrl != null) {
					embedBuilder.ImageUrl = copyEmbedBuilder.ImageUrl;
				} else if (embedBuilder.ImageUrl == null && copyEmbedBuilder.Url != null && copyEmbedBuilder.Url.StartsWith("https://cdn.discordapp.com/attachments/")) {
					embedBuilder.ImageUrl = copyEmbedBuilder.Url;
				}
			}
			
			dmb.AddEmbed(embedBuilder);

			if (copyEmbed != null) {
				dmb.AddEmbed(copyEmbed);
			}
		};
	}
}
