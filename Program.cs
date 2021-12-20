using System.Dynamic;
using System.Linq;
using System.Text;
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
					isc.Configure<DiscordConfiguration>(hbc.Configuration.GetSection("Discord"));
					isc.Configure<ConnectionStringsConfiguration>(hbc.Configuration.GetSection("ConnectionStrings"));
					
					isc.AddSingleton(isp => {
						var config = isp.GetRequiredService<IOptions<DiscordConfiguration>>().Value;
						config.Intents = DiscordIntents.GuildMessageReactions;
						config.LoggerFactory = isp.GetRequiredService<ILoggerFactory>();
						return new DiscordClient(config);
					});

					isc.ConfigureDbContext<FridgeDbContext>();
				})
				.Build();

			Program.Host = host;

			var discord = host.Services.GetRequiredService<DiscordClient>();

			discord.MessageReactionAdded += (client, ea) => {
				_ = OnReactionAddedAsync(client, ea);
				return Task.CompletedTask;
			};

			discord.MessageReactionRemoved += (client, ea) => {
				_ = OnReactionRemovedAsync(client, ea);
				return Task.CompletedTask;
			};
			
			discord.MessageCreated += (client, ea) => {
				_ = OnMessageCreatedAsync(client, ea);
				return Task.CompletedTask;
			}
			
			await discord.ConnectAsync();

			await host.RunAsync();
		}

		private static async Task OnReactionAddedAsync(DiscordClient discordClient, MessageReactionAddEventArgs ea) {
			using FridgeDbContext dbcontext = Host.Services.GetRequiredService<FridgeDbContext>();
			ServerEmote? serverEmote = await dbcontext.Emotes.FindAsync(ea.Emoji.Id, ea.Guild.Id);
			if (serverEmote != null) {
				DiscordReaction messageReaction = ea.Message.Reactions.First(mr => mr.Emoji == ea.Emoji);
				if (messageReaction.Count >= serverEmote.MinimumToAdd) {
					// TODO find a way to skip intermediate discord api calls and send/update the message directly
					DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId)!;
					// TODO eager load emotes
					FridgeEntry? fridgeEntry = serverEmote.Server.FridgeEntries.FirstOrDefault(entry => entry.MessageId == ea.Message.Id);
					if (fridgeEntry != null) {
						FridgeEntryEmote? entryEmote = fridgeEntry.Emotes.FirstOrDefault(fee => fee.EmoteId == ea.Emoji.Id);
						if (entryEmote == null) {
							fridgeEntry.Emotes.Add(new FridgeEntryEmote() {
								EmoteId = ea.Emoji.Id
							});
						}
						
						// TODO handle message deletion
						DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
						await fridgeMessage.ModifyAsync(dmb => GetFridgeMessageBuilder(fridgeEntry, ea.Message));
					} else {
						fridgeEntry = new FridgeEntry() {
							ChannelId = ea.Channel.Id,
							MessageId = ea.Message.Id,
							ServerId = ea.Guild.Id,
							Emotes = new List<FridgeEntryEmote>() {
								new FridgeEntryEmote() {
									EmoteId = ea.Emoji.Id
								}
							}
						};
						dbcontext.Entries.Add(fridgeEntry);

						await fridgeChannel.SendMessageAsync(GetFridgeMessageBuilder(fridgeEntry, ea.Message));
					}
				}
			}

			await dbcontext.SaveChangesAsync();
		}

		private static async Task OnReactionRemovedAsync(DiscordClient discordClient, MessageReactionRemoveEventArgs ea) {
			using FridgeDbContext dbcontext = Host.Services.GetRequiredService<FridgeDbContext>();
			ServerEmote? serverEmote = await dbcontext.Emotes.FindAsync(ea.Emoji.Id, ea.Guild.Id);
			if (serverEmote != null) {
				DiscordReaction messageReaction = ea.Message.Reactions.First(mr => mr.Emoji == ea.Emoji);
				if (messageReaction.Count < serverEmote.MaximumToRemove) {
					// TODO eager load emotes
					FridgeEntry? fridgeEntry = serverEmote.Server.FridgeEntries.FirstOrDefault(entry => entry.MessageId == ea.Message.Id);
					if (fridgeEntry != null) {
						FridgeEntryEmote? entryEmote = fridgeEntry.Emotes.FirstOrDefault(fee => fee.EmoteId == ea.Emoji.Id);
						if (entryEmote != null) {
							fridgeEntry.Emotes.Remove(entryEmote);
							
							// TODO find a way to skip intermediate discord api calls and update/delete the message directly
							DiscordChannel fridgeChannel = await discordClient.GetChannelAsync(serverEmote.Server.ChannelId)!;
							if (fridgeEntry.Emotes.Count == 0) {
								// TODO handle message deletion
								DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
								await fridgeChannel.DeleteMessageAsync(fridgeMessage);
							} else {
								// TODO handle message deletion
								DiscordMessage fridgeMessage = await fridgeChannel.GetMessageAsync(fridgeEntry.FridgeMessageId)!;
								await fridgeMessage.ModifyAsync(GetFridgeMessageBuilder(fridgeEntry, ea.Message));
							}
						}
					}
				}
			}

			await dbcontext.SaveChangesAsync();
		}

		private static Action<DiscordMessageBuilder> GetFridgeMessageBuilder(FridgeEntry entry, DiscordMessage message) {
			return (dmb) => {
				var author = (DiscordMember) message.Author;
				
				var content = new StringBuilder();

				
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
					ImageUrl = message.Attachments.FirstOrDefault()?.Url,
					Timestamp = message.Timestamp,
					Url = message.JumpLink.ToString()
				};

				if (message.ReferencedMessage != null) {
					string fieldName = "Replying to a message from " + ((DiscordMember) message.ReferencedMessage.Author).Nickname;
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