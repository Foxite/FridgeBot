using System.Drawing;
using System.Text;
using Foxite.Common.Notifications;
using Microsoft.Extensions.Logging;
using Revcord;
using Revcord.Entities;

namespace FridgeBot;

public class RevcordFridgeTarget : IFridgeTarget {
	private readonly ILogger<RevcordFridgeTarget> m_Logger;
	private readonly NotificationService m_Notifications;
	private readonly HttpClient m_Http;
	private readonly ChatClient m_ChatClient;

	public RevcordFridgeTarget(ILogger<RevcordFridgeTarget> logger, NotificationService notifications, HttpClient http, ChatClient chatClient) {
		m_Logger = logger;
		m_Notifications = notifications;
		m_Http = http;
		m_ChatClient = chatClient;
	}
		
	public async Task<EntityId> CreateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) {
		// TODO find a way to skip intermediate discord api calls and send/update/delete the message directly
		IChannel fridgeChannel = await m_ChatClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		IMessage fridgeMessage = await fridgeChannel.SendMessageAsync(GetFridgeMessageBuilder(message, fridgeEntry, null));
		return fridgeMessage.Id;
	}
	
	public async Task UpdateFridgeMessageAsync(FridgeEntry fridgeEntry, IMessage message) {
		IChannel fridgeChannel = await m_ChatClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		try {
			IMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
			await fridgeMessage.UpdateAsync(GetFridgeMessageBuilder(message, fridgeEntry, fridgeMessage));
		} catch (EntityNotFoundException ex) {
			throw new FileNotFoundException("The fridge message has been deleted externally", ex);
		}
	}
	
	public async Task DeleteFridgeMessageAsync(FridgeEntry fridgeEntry) {
		IChannel fridgeChannel = await m_ChatClient.GetChannelAsync(fridgeEntry.Server.ChannelId);
		try {
			IMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId);
			await fridgeMessage.DeleteAsync();
		} catch (EntityNotFoundException) { }
	}
	
	private MessageBuilder GetFridgeMessageBuilder(IMessage message, FridgeEntry fridgeEntry, IMessage? existingFridgeMessage) {
		var messageBuilder = new MessageBuilder();

		messageBuilder.WithContent("Placeholder");

		return messageBuilder;
	}

#if false
	private MessageBuilder GetFridgeMessageBuilder(IMessage message, FridgeEntry fridgeEntry, IMessage? existingFridgeMessage) {
		var messageBuilder = new MessageBuilder();
		
		string? replyingToNickname = null;
		if (message.ReferencedMessage != null) {
			if (message.ReferencedMessage.Author is IGuildMember replyingToMember && !string.IsNullOrEmpty(replyingToMember.Nickname)) {
				replyingToNickname = replyingToMember.Nickname;
			} else {
				replyingToNickname = message.ReferencedMessage.Author.Username;
			}
		}
		
		var reactions = new Dictionary<IEmoji, int>();
		foreach (IReaction reaction in message.Reactions) {
			if (fridgeEntry.Emotes.Any(emote => emote.EmoteString == reaction.Emoji.ToString())) {
				reactions[reaction.Emoji] = reaction.Count;
			}
		}
			
		var content = new StringBuilder();
		int i = 0;
		foreach ((IEmoji? emoji, _) in reactions) {
			if (i > 0) {
				content.Append(i == reactions.Count - 1 ? " & " : ", ");
			}
			content.Append(emoji.ToString()); // String should not be normalized here because it gets sent to discord, rather than just stored in the database.
			i++;
		}

		content.AppendLine(" moment in " + message.Channel.MentionString + "!");
			
		foreach ((IEmoji? emoji, int count) in reactions) {
			content.AppendLine($"{emoji.ToString()} x{count}"); // See above
		}
			
		messageBuilder.Content = content.ToString();

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

		// TODO move this into Revcord
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
				IAttachment? videoAttachment = message.Attachments.FirstOrDefault(att => att.MediaType != null && att.MediaType.StartsWith("video/"));
				if (videoAttachment != null && (existingFridgeMessage == null || existingFridgeMessage.Attachments.Count == 0)) {
					try {
						using Stream download = m_Http.GetStreamAsync(videoAttachment.Url).Result;
						var memory = new MemoryStream();
						download.CopyTo(memory);
						memory.Position = 0;
						messageBuilder.AddFile(videoAttachment.FileName, memory);
					} catch (Exception e) {
						m_Logger.LogError(e, "Error downloading attachment {videoUrl}", videoAttachment.Url);
						m_Notifications.SendNotificationAsync($"Error downloading attachment {videoAttachment.Url}, ignoring", e);
					}
				}
			}
		}

		var embedBuilder = new EmbedBuilder() {
			Author = new DiscordEmbedBuilder.EmbedAuthor() {
				Name = authorName,
				IconUrl = authorAvatarUrl
			},
			Color = Color.FromArgb(0x7F007FFF | (1 << 31)), // have to do this because 0xFF007FFF makes the compiler think i want a uint, and System.Drawing doesn't want a uint
			Description = description,
			Footer = new DiscordEmbedBuilder.EmbedFooter() {
				Text = message.Id.ToString()
			},
			ImageUrl = imageUrl,
			Timestamp = message.CreationTimestamp,
			Url = message.JumpLink.ToString()
		};
			
		embedBuilder.AddField("Jump to message", Formatter.MaskedUrl("Click here to jump", new Uri(message.JumpLink)));

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
			} else if (copyEmbedBuilder.ImageUrl != null && embedBuilder.ImageUrl == null) {
				embedBuilder.ImageUrl = copyEmbedBuilder.ImageUrl;
			}
		}
			
		messageBuilder.AddEmbed(embedBuilder);

		if (copyEmbed != null) {
			messageBuilder.AddEmbed(copyEmbed);
		}

		return messageBuilder;
	}
#endif
}
