using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;

namespace FridgeBot {
	public static class Program {
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
				.ConfigureServices((hbc, isc) => {
					//isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
					isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
					
					isc.AddSingleton(isp => {
						var config = new DiscordConfiguration {
							Token = hbc.Configuration.GetSection("Discord").GetValue<string>("Token"),
							Intents = DiscordIntents.All, // Not sure which one, but there is an intent that is necessary to get the permissions of any user.
							LoggerFactory = isp.GetRequiredService<ILoggerFactory>(),
							MinimumLogLevel = LogLevel.Information
						};
						return new DiscordClient(config);
					});

					isc.AddSingleton<CommandService>();

					isc.AddDbContext<FridgeDbContext>((isp, dbcob) => {
						ConnectionStringsConfiguration config = isp.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value;
						string connectionString = config.GetConnectionString<FridgeDbContext>();

						_ = config.Mode switch {
							ConnectionStringsConfiguration.Backend.Sqlite => dbcob.UseSqlite(connectionString),
							ConnectionStringsConfiguration.Backend.Postgres => dbcob.UseNpgsql(connectionString),
						};
					}, ServiceLifetime.Transient);
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
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}

		private static async Task OnMessageCreatedAsync(DiscordClient discordClient, MessageCreateEventArgs ea) {
			DiscordUser? firstMentionedUser = ea.Message.MentionedUsers.FirstOrDefault();
			if (firstMentionedUser != null && (((DiscordMember) ea.Message.Author).Permissions & Permissions.Administrator) != 0 && !ea.Author.IsBot && firstMentionedUser.Id == discordClient.CurrentUser.Id && ea.Message.Content.StartsWith("<@")) {
				var commands = Host.Services.GetRequiredService<CommandService>();
				string input = ea.Message.Content[(discordClient.CurrentUser.Mention.Length + 1)..];
				Console.WriteLine(input);
				IResult result = await commands.ExecuteAsync(input, new DiscordCommandContext(Host.Services, ea.Message));
				await ea.Message.RespondAsync(result.ToString());
				if (result is CommandExecutionFailedResult cefr) {
					Console.WriteLine(cefr.Exception);
				}
			}
		}

		private static async Task OnReactionModifiedAsync(DiscordClient discordClient, DiscordMessage message, DiscordEmoji emoji, bool added) {
			using FridgeDbContext dbcontext = Host.Services.GetRequiredService<FridgeDbContext>();
			ServerEmote? serverEmote = await dbcontext.Emotes.Include(emote => emote.Server).FirstOrDefaultAsync(emote => emote.ServerId == message.Channel.GuildId && emote.EmoteId == emoji.Id);
			if (serverEmote != null) {
				message = await message.Channel.GetMessageAsync(message.Id); // refresh message along with its reactions
				DiscordReaction? messageReaction = message.Reactions.FirstOrDefault(mr => mr.Emoji.Id == emoji.Id);

				// TODO find a way to skip intermediate discord api calls and update/delete the message directly
				DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId);
				FridgeEntry? fridgeEntry = await dbcontext.Entries.Include(entry => entry.Emotes).FirstOrDefaultAsync(entry => entry.ServerId == message.Channel.GuildId && entry.MessageId == message.Id);
				FridgeEntryEmote? entryEmote = fridgeEntry?.Emotes.FirstOrDefault(fee => fee.EmoteId == emoji.Id);

				if (added) {
					Debug.Assert(messageReaction != null);
					if (entryEmote == null && messageReaction.Count >= serverEmote.MinimumToAdd) {
						entryEmote = new FridgeEntryEmote() {
							EmoteId = emoji.Id
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

				if (entryEmote != null) {
					Debug.Assert(fridgeEntry != null);
					if (fridgeEntry.Emotes.Count == 0) {
						dbcontext.Entries.Remove(fridgeEntry);
						// TODO handle message deletion
						DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
						await fridgeChannel.DeleteMessageAsync(fridgeMessage);
					} else if (fridgeEntry.FridgeMessageId == 0) {
						DiscordMessage fridgeMessage = await fridgeChannel.SendMessageAsync(await GetFridgeMessageBuilderAsync(fridgeEntry, message, dbcontext));
						fridgeEntry.FridgeMessageId = fridgeMessage.Id;
						dbcontext.Entries.Add(fridgeEntry);
					} else {
						// TODO handle message deletion
						DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
						await fridgeMessage.ModifyAsync(await GetFridgeMessageBuilderAsync(fridgeEntry, message, dbcontext));
					}
				}
			}

			await dbcontext.SaveChangesAsync();
		}

		private static async Task<Action<DiscordMessageBuilder>> GetFridgeMessageBuilderAsync(FridgeEntry entry, DiscordMessage message, FridgeDbContext context) {
			string replyingToNickname = (await message.Channel.Guild.GetMemberAsync(message.ReferencedMessage.Author.Id)).Nickname;
			return (dmb) => {
				var author = (DiscordMember) message.Author;
				
				var content = new StringBuilder();
				foreach ((DiscordReaction reaction, FridgeEntryEmote emote) in message.Reactions
					.Join(entry.Emotes, reaction => reaction.Emoji.Id, emote => emote.EmoteId, (reaction, emote) => (reaction, emote))
					.Where(tuple => entry.Emotes.Any(emote => emote.EmoteId == tuple.emote.EmoteId))
				) {
					content.AppendLine($"{reaction.Count}x {reaction.Emoji.ToString()}");
				}
				
				dmb.Content = content.ToString();

				var embedBuilder = new DiscordEmbedBuilder() {
					Author = new DiscordEmbedBuilder.EmbedAuthor() {
						Name = author.Nickname,
						IconUrl = author.GuildAvatarUrl
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
				
				string fieldName = "Jump to message";
				if (fieldName.Length > 255) {
					fieldName = fieldName[..255];
				}
				embedBuilder.AddField(fieldName, $"[Click here to jump]({message.JumpLink})");

				if (message.ReferencedMessage != null) {
					fieldName = "Replying to a message from " + replyingToNickname;
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
