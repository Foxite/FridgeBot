using System.Drawing;
using System.Text;
using Foxite.Common.Notifications;
using Microsoft.Extensions.Logging;
using Revcord;
using Revcord.Entities;
using Revcord.Revolt;

namespace FridgeBot;

public class RevoltFridgeTarget : BaseFridgeTarget {
	private readonly HttpClient m_Http;

	public RevoltFridgeTarget(ILogger<RevoltFridgeTarget> logger, NotificationService notifications, HttpClient http) : base(logger, notifications) {
		m_Http = http;
	}

	public override bool Supports(ChatClient client) => client is RevoltChatClient;
	
	protected async override Task<EntityId> ExecuteCreateAsync(FridgeEntry entry, IMessage message) {
		IChannel fridgeChannel = await message.Client.GetChannelAsync(entry.FridgeMessageId);
		IMessage fridgeMessage = await fridgeChannel.SendMessageAsync(GetFridgeMessageBuilder(entry, message, null));
		return fridgeMessage.Id;
	}

	protected override Task ExecuteUpdateAsync(FridgeEntry entry, IMessage message, IMessage existingFridgeMessage) {
		return existingFridgeMessage.UpdateAsync(GetFridgeMessageBuilder(entry, message, existingFridgeMessage));
	}
	
	private MessageBuilder GetFridgeMessageBuilder(FridgeEntry fridgeEntry, IMessage message, IMessage? existingFridgeMessage) {
		var messageBuilder = new MessageBuilder();
		
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
			content.Append(emoji.ToString());
			i++;
		}

		content.AppendLine(" moment in " + message.Channel.MentionString + "!");
			
		foreach ((IEmoji? emoji, int count) in reactions) {
			content.AppendLine($"{emoji.ToString()} x{count}");
		}
		
		messageBuilder.Content = content.ToString();

		var embedBuilder = new EmbedBuilder()
			.WithTitle(message.Author.Username)
			.WithIconUrl(message.Author.AvatarUrl)
			.WithColor(Color.FromArgb(0x7F007FFF | (1 << 31)))
			.WithDescription(message.Content);
		
		//embedBuilder.AddField("Jump to message", Formatter.MaskedUrl("Click here to jump", new Uri(message.JumpLink)));
		embedBuilder.AddField(new EmbedFieldBuilder("Jump to message", $"[Click here to jump]({message.JumpLink})"));

		messageBuilder.AddEmbed(embedBuilder);

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
