using System.Diagnostics;
using System.Text;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Foxite.Common;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FridgeBot {
	public sealed class Program {
		public static IHost Host { get; set; }

		private static IHostBuilder CreateHostBuilder(string[] args) =>
			Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration((hostingContext, configuration) => {
					configuration.Sources.Clear();

					configuration
						.AddJsonFile("appsettings.json", true, true)
						.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true)
						.AddEnvironmentVariables("FRIDGE_")
						.AddCommandLine(args);
				});
		
		private static async Task Main(string[] args) {
			using IHost host = CreateHostBuilder(args)
				.ConfigureLogging((_, builder) => {
					builder.AddExceptionDemystifyer();
				})
				.ConfigureServices((hbc, isc) => {
					//isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
					isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
					
					isc.AddSingleton(isp => {
						var config = new DiscordConfiguration {
							Token = hbc.Configuration.GetSection("Discord").GetValue<string>("Token"),
							Intents = DiscordIntents.GuildMessages | DiscordIntents.GuildMembers | DiscordIntents.GuildMessageReactions | DiscordIntents.Guilds,
							LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
							MinimumLogLevel = LogLevel.Information,
							MessageCacheSize = 0
						};
						return new DiscordClient(config);
					});

					isc.AddSingleton<HttpClient>();
					
					isc.ConfigureDbContext<FridgeDbContext>();
					
					isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));
				})
				.Build();

			Host = host;

			await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
				await dbContext.Database.MigrateAsync();
			}

			var discord = host.Services.GetRequiredService<DiscordClient>();
			var commands = discord.UseCommandsNext(new CommandsNextConfiguration() {
				EnableMentionPrefix = true,
				EnableDefaultHelp = true,
				EnableDms = false,
				Services = host.Services,
			});
			commands.RegisterCommands<AdminModule>();
			commands.CommandErrored += async (_, ea) => {
				switch (ea.Exception) {
					case CommandNotFoundException:
						await ea.Context.RespondAsync("Command not found");
						return;
					case ChecksFailedException:
						await ea.Context.RespondAsync("Checks failed 🙁");
						return;
					case ArgumentException { Message: "Could not find a suitable overload for the command." }:
						await ea.Context.RespondAsync("Invalid arguments.");
						return;
				}

				string N(object? o) => o?.ToString() ?? "null";
				string errorMessage =
					$"Exception in OnMessageCreated\n" +
					$"author: {N(ea.Context.User?.Id)} ({N(ea.Context.User?.Username)}#{N(ea.Context.User?.Discriminator)}), bot: {N(ea.Context.User?.IsBot)}\n" +
					$"message: {N(ea.Context.Message?.Id)} ({N(ea.Context.Message?.JumpLink)}), type: {N(ea.Context.Message?.MessageType?.ToString() ?? "(null)")}, webhook: {N(ea.Context.Message?.WebhookMessage)}\n" +
					$"channel {N(ea.Context.Channel?.Id)} ({N(ea.Context.Channel?.Name)})\n" +
					$"{(ea.Context.Channel?.Guild != null ? $"guild {N(ea.Context.Channel?.Guild?.Id)} ({N(ea.Context.Channel?.Guild?.Name)})" : "")}";
				Host.Services.GetRequiredService<ILogger<Program>>().LogCritical(ea.Exception, errorMessage);
				await ea.Context.RespondAsync("Internal error, devs notified.");
				
				await Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync(errorMessage, ea.Exception.Demystify());
			};

			discord.MessageReactionAdded += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, true);
			discord.MessageReactionRemoved += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, false);

			discord.ClientErrored += (_, eventArgs) => Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}
	}

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

	public interface IFridgeTarget {
		
	}

	public class DiscordFridgeTarget : IFridgeTarget {
		private readonly HttpClient m_Http;
		private readonly ILogger<Program> m_Logger;
		private readonly NotificationService m_NotificationService;
		
		public DiscordFridgeTarget(HttpClient http, ILogger<Program> logger, NotificationService notificationService) {
			m_Http = http;
			m_Logger = logger;
			m_NotificationService = notificationService;
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
					MessageType.ChannelNameChange => "ChannelNameChange", // should not happen in guilds
					MessageType.ChannelIconChange => "ChannelIconChange", // should not happen in guilds
					MessageType.GuildDiscoveryDisqualified => "GuildDiscoveryDisqualified",
					MessageType.GuildDiscoveryRequalified => "GuildDiscoveryRequalified",
					MessageType.GuildDiscoveryGracePeriodInitialWarning => "GuildDiscoveryGracePeriodInitialWarning",
					MessageType.GuildDiscoveryGracePeriodFinalWarning => "GuildDiscoveryGracePeriodFinalWarning",
					MessageType.GuildInviteReminder => "GuildInviteReminder",
					MessageType.ContextMenuCommand => "ContextMenuCommand",
					_ => ""
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
								m_NotificationService.SendNotificationAsync($"Error downloading attachment {videoAttachment.Url}, ignoring", e);
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
				
				embedBuilder.AddField("Jump to message", $"[Click here to jump]({message.JumpLink})");

				if (message.ReferencedMessage != null) {
					string fieldName = "Replying to " + replyingToNickname;
					if (fieldName.Length > 255) {
						fieldName = fieldName[..255];
					}
					embedBuilder.AddField(fieldName, $"[Click here to jump]({message.ReferencedMessage.JumpLink})");
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
				
				dmb.AddEmbed(embedBuilder);

				if (copyEmbed != null) {
					dmb.AddEmbed(copyEmbed);
				}
			};
		}
	}
}
