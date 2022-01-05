using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Foxite.Common.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
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
				.ConfigureLogging((context, builder) => {
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
						};
						return new DiscordClient(config);
					});

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
			
			discord.MessageReactionAdded += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, ea.User, true);
			discord.MessageReactionRemoved += (client, ea) => OnReactionModifiedAsync(client, ea.Message, ea.Emoji, ea.User, false);

			discord.MessageCreated += OnMessageCreatedAsync;

			discord.ClientErrored += (sender, eventArgs) => Host.Services.GetRequiredService<NotificationService>().SendNotificationAsync($"Exception in {eventArgs.EventName}", eventArgs.Exception);
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}

		private static async Task OnMessageCreatedAsync(DiscordClient discordClient, MessageCreateEventArgs ea) {
			DiscordUser? firstMentionedUser = ea.Message.MentionedUsers.FirstOrDefault();
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

		private static async Task OnReactionModifiedAsync(DiscordClient discordClient, DiscordMessage message, DiscordEmoji emoji, DiscordUser user, bool added) {
			if (user.IsCurrent) {
				return;
			}
			
			await using var dbcontext = Host.Services.GetRequiredService<FridgeDbContext>();

			FridgeEntry? fridgeEntry = null;
			DiscordMessage? fridgeMessage = null;
			// Acquire additional data such as the author, and refresh reaction counts
			message = await message.Channel.GetMessageAsync(message.Id);
			if (message.Author.IsCurrent) {
				fridgeEntry = await dbcontext.Entries.Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.FridgeMessageId == message.Id && entry.ServerId == message.Channel.GuildId);
				if (fridgeEntry != null) {
					// If it's a reaction on our own fridge message, then treat it as a reaction on the fridged message
					fridgeMessage = message;
					DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(fridgeEntry.ChannelId);
					message = await fridgeChannel.GetMessageAsync(fridgeEntry.MessageId);
				} else {
					// It's our message but does not appear to be a fridge message
					return;
				}
			}
			
			ServerEmote? serverEmote = await dbcontext.Emotes.Include(emote => emote.Server).Where(emote => emote.ServerId == message.Channel.GuildId).FirstOrDefaultAsync(emote => emote.EmoteString == emoji.ToStringInvariant());
			if (serverEmote != null) {
				fridgeEntry ??= await dbcontext.Entries.Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.MessageId == message.Id && entry.ServerId == message.Channel.GuildId);
				
				FridgeEntryEmote? entryEmote = fridgeEntry?.Emotes.FirstOrDefault(fee => fee.EmoteString == emoji.ToStringInvariant());
				
				int count = 0;
				DiscordReaction? messageReaction = message.Reactions.FirstOrDefault(reaction => reaction.Emoji == emoji);
				if (messageReaction != null) {
					count += messageReaction.Count - (messageReaction.IsMe ? 1 : 0);
				}
				if (fridgeMessage != null) {
					DiscordReaction? fridgeMessageReaction = fridgeMessage.Reactions.FirstOrDefault(reaction => reaction.Emoji == emoji);
					if (fridgeMessageReaction != null) {
						count += fridgeMessageReaction.Count - (fridgeMessageReaction.IsMe ? 1 : 0);
					}
				}

				if (added) {
					if (entryEmote == null && count >= serverEmote.MinimumToAdd) {
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
					if (entryEmote != null && count <= serverEmote.MaximumToRemove) {
						Debug.Assert(fridgeEntry != null);
						fridgeEntry.Emotes.Remove(entryEmote);
					}
				}

				if (fridgeEntry != null) {
					// TODO handle message deletion
					if (fridgeEntry.Emotes.Count == 0) {
						dbcontext.Entries.Remove(fridgeEntry);
						if (fridgeMessage == null) {
							DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
							fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
						}
						// TODO find a way to skip intermediate discord api calls and send/update/delete the message directly
						await fridgeMessage.DeleteAsync();
					} else {
						IEnumerable<DiscordReaction> allReactions = message.Reactions;
						if (fridgeMessage == null && fridgeEntry.FridgeMessageId != 0) {
							DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
							fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
						}
						if (fridgeMessage != null) {
							// I'd like to make sure that if a user adds a reaction with the same emote to both the message and the fridge message, it doesn't count.
							// However that requires a whole bunch of API calls and I don't think it's worth the extra work.
							allReactions = allReactions.Concat(fridgeMessage.Reactions);
						}

						var fridgeableEmotes = new Dictionary<DiscordEmoji, int>();
						foreach (DiscordReaction reaction in allReactions) {
							if (fridgeEntry.Emotes.Any(emote => emote.EmoteString == reaction.Emoji.ToStringInvariant()) && !fridgeableEmotes.TryAdd(reaction.Emoji, reaction.Count - (reaction.IsMe ? 1 : 0))) {
								fridgeableEmotes[reaction.Emoji] += reaction.Count - (reaction.IsMe ? 1 : 0);
							}
						}

						if (fridgeEntry.FridgeMessageId == 0) {
							DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
							fridgeMessage = await fridgeChannel.SendMessageAsync(await GetFridgeMessageBuilderAsync(message, fridgeableEmotes));
							fridgeEntry.FridgeMessageId = fridgeMessage.Id;
							dbcontext.Entries.Add(fridgeEntry);
						} else {
							// TODO handle message deletion
							Debug.Assert(fridgeMessage != null);
							await fridgeMessage.ModifyAsync(await GetFridgeMessageBuilderAsync(message, fridgeableEmotes));
						}

						List<DiscordEmoji> existingReactions = fridgeMessage.Reactions.Select(reaction => reaction.Emoji).ToList();
						List<DiscordEmoji> desiredReactions = fridgeableEmotes.Select(kvp => kvp.Key).ToList();
						
						foreach (DiscordEmoji unwantedEmoji in existingReactions.Except(desiredReactions)) {
							await fridgeMessage.DeleteOwnReactionAsync(unwantedEmoji);
						}

						foreach (DiscordEmoji neededEmoji in desiredReactions.Except(existingReactions)) {
							await fridgeMessage.CreateReactionAsync(neededEmoji);
						}
					}
				}
			}

			await dbcontext.SaveChangesAsync();
		}

		private static async Task<Action<DiscordMessageBuilder>> GetFridgeMessageBuilderAsync(DiscordMessage message, Dictionary<DiscordEmoji, int> reactions) {
			// This needs to be done here because of the async call (the builder lambda cannot be async)
			string? replyingToNickname = null;
			if (message.ReferencedMessage != null) {
				DiscordMember replyingToMember = await message.Channel.Guild.GetMemberAsync(message.ReferencedMessage.Author.Id);
				replyingToNickname = string.IsNullOrEmpty(replyingToMember.Nickname) ? replyingToMember.Username : replyingToMember.Nickname;
			}
			
			return dmb => {
				var author = (DiscordMember) message.Author;

				var content = new StringBuilder();
				int i = 0;
				foreach ((DiscordEmoji? emoji, _) in reactions) {
					if (i > 0) {
						if (i == reactions.Count - 1) {
							content.Append(" & ");
						} else {
							content.Append(", ");
						}
					}
					content.Append(emoji.ToString()); // String should not be normalized here because it gets sent to discord, rather than just stored in the database.
					i++;
				}

				content.AppendLine(" moment in " + message.Channel.Mention + "!");
				
				foreach ((DiscordEmoji? emoji, int count) in reactions) {
					content.AppendLine($"{emoji.ToString()} x{count}"); // See above
				}
				
				dmb.Content = content.ToString();

				var embedBuilder = new DiscordEmbedBuilder() {
					Author = new DiscordEmbedBuilder.EmbedAuthor() {
						Name = author.Nickname ?? author.Username,
						IconUrl = author.AvatarHash == author.GuildAvatarHash ? author.AvatarUrl : author.GuildAvatarUrl
					},
					Color = new Optional<DiscordColor>(DiscordColor.Azure),
					Description = message.Content, // No need to check the length because the max length of a discord message is 4000 with nitro, but the max length of an embed description is 4096.
					Footer = new DiscordEmbedBuilder.EmbedFooter() {
						Text = message.Id.ToString()
					},
					ImageUrl = message.Attachments?.FirstOrDefault()?.Url,
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
