using System.Diagnostics;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qmmands;

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
							Intents = DiscordIntents.All, // Not sure which one, but there is an intent that is necessary to get the permissions of any user.
							LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
							MinimumLogLevel = LogLevel.Information,
							MessageCacheSize = 0
						};
						return new DiscordClient(config);
					});

					isc.AddSingleton<HttpClient>();
					isc.AddSingleton<CommandService>();
					
					isc.ConfigureDbContext<FridgeDbContext>();
					
					isc.AddNotifications().AddDiscord(hbc.Configuration.GetSection("DiscordNotifications"));
				})
				.Build();

			Host = host;

			await using (var dbContext = host.Services.GetRequiredService<FridgeDbContext>()) {
				await dbContext.Database.MigrateAsync();
			}

			var commands = host.Services.GetRequiredService<CommandService>();
			commands.AddModule<FridgeCommandModule>();
			commands.AddTypeParser(new ChannelParser());
			commands.AddTypeParser(new DiscordEmojiParser());

			var discord = host.Services.GetRequiredService<DiscordClient>();
			
			discord.MessageReactionAdded += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, true);
			discord.MessageReactionRemoved += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, false);

			discord.MessageCreated += OnMessageCreatedAsync;

			discord.ClientErrored += (_, eventArgs) => Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}

		private static async Task OnMessageCreatedAsync(DiscordClient discordClient, MessageCreateEventArgs ea) {
			DiscordUser? firstMentionedUser = ea.Message.MentionedUsers.Count >= 1 ? ea.Message.MentionedUsers[0] : null;
			if (firstMentionedUser != null && (((DiscordMember) ea.Message.Author).Permissions & Permissions.Administrator) != 0 && !ea.Author.IsBot && firstMentionedUser.Id == discordClient.CurrentUser.Id && ea.Message.Content.StartsWith("<@")) {
				var commands = Host.Services.GetRequiredService<CommandService>();
				string input = ea.Message.Content[(discordClient.CurrentUser.Mention.Length + 1)..];
				IResult result = await commands.ExecuteAsync(input, new DiscordCommandContext(Host.Services, ea.Message));
				await ea.Message.RespondAsync(result.ToString());
				if (result is CommandExecutionFailedResult cefr) {
					Host.Services.GetRequiredService<ILogger<Program>>().LogCritical(cefr.Exception, "Error executing: {}", input);
				}
			}
		}

		private static async Task OnReactionModifiedAsync(DiscordClient discordClient, DiscordMessage message, DiscordEmoji emoji, bool added) {
			// Acquire additional data such as the author, and refresh reaction counts
			message = await message.Channel.GetMessageAsync(message.Id);
			
			if (message.Author.IsCurrent) {
				return;
			}

			FridgeDbContext? dbcontext = null;
			try {
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
			} catch (Exception e) {
				throw new Exception($"{message.Author.Id} ({message.Author.Username}#{message.Author.Discriminator}), bot: {message.Author.IsBot}\n" +
									$"message: {message.Id} ({message.JumpLink}), type: {message.MessageType?.ToString() ?? "(null)"}, webhook: {message.WebhookMessage}\n" +
									$"channel {message.Channel.Id} ({message.Channel.Name})\n" +
									(message.Channel.Guild != null ? $"guild {message.Channel.Guild.Id} ({message.Channel.Guild.Name})" : ""), e);
			} finally {
				if (dbcontext != null) {
					await dbcontext.SaveChangesAsync();
					await dbcontext.DisposeAsync();
				}
			}
		}

		private static Action<DiscordMessageBuilder> GetFridgeMessageBuilder(DiscordMessage message, FridgeEntry fridgeEntry, DiscordMessage? existingFridgeMessage) {
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
				if (message.Author is DiscordMember authorMember) {
					authorName = authorMember.Nickname ?? authorMember.Username;
					authorAvatarUrl = authorMember.GuildAvatarUrl ?? authorMember.AvatarUrl;
				} else {
					authorName = message.Author.Username;
					authorAvatarUrl = message.Author.AvatarUrl;
				}

				string description = message.MessageType switch {
					MessageType.Default or MessageType.Reply => message.Content, // No need to check the length because the max length of a discord message is 4000 with nitro, but the max length of an embed description is 4096.
					MessageType.ChannelPinnedMessage => $"{authorName} pinned a message to the channel.",
					MessageType.ApplicationCommand => $"{authorName} used ${message.Interaction.Name}",
					MessageType.GuildMemberJoin => $"{authorName} has joined the server!",
					MessageType.UserPremiumGuildSubscription => $"{authorName} has just boosted the server!",
					MessageType.TierOneUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 1**!",
					MessageType.TierTwoUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 2**!",
					MessageType.TierThreeUserPremiumGuildSubscription => $"{authorName} has just boosted the server! {message.Channel.Guild.Name} has achieved **Level 3**!",
					MessageType.RecipientAdd => $"{authorName} joined the thread!", // Does not actually seem to happen
					MessageType.RecipientRemove => $"{authorName} removed {(message.MentionedUsers[0] as DiscordMember)?.Nickname ?? message.MentionedUsers[0].Username} from the thread.",
					_ => ""
				};

				string? imageUrl = null;
				if (message.Attachments.Count > 0) {
					imageUrl = message.Attachments.FirstOrDefault(att => att.MediaType != null && att.MediaType.StartsWith("image/"))?.Url;
					if (imageUrl == null) {
						DiscordAttachment? videoAttachment = message.Attachments.FirstOrDefault(att => att.MediaType != null && att.MediaType.StartsWith("video/"));
						if (videoAttachment != null && (existingFridgeMessage == null || existingFridgeMessage.Attachments.Count == 0)) {
							var http = Host.Services.GetRequiredService<HttpClient>();
							try {
								using Stream download = http.GetStreamAsync(videoAttachment.Url).Result;
								var memory = new MemoryStream();
								download.CopyTo(memory);
								memory.Position = 0;
								dmb.WithFile(videoAttachment.FileName, memory);
							} catch (Exception e) {
								Host.Services.GetRequiredService<ILogger<Program>>().LogError(e, "Error downloading attachment {videoUrl}", videoAttachment.Url);
								Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync($"Error downloading attachment {videoAttachment.Url}, ignoring", e);
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

				dmb.AddEmbed(embedBuilder);
			};
		}
	}
}
