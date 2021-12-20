using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot {
	public class DiscordCommandContext : CommandContext {
		public DiscordMessage Message { get; }
		public DiscordChannel Channel { get; }
		public DiscordGuild Guild { get; }
		public DiscordMember User { get; }

		public DiscordCommandContext(IServiceProvider serviceProvider, DiscordMessage message) : base(serviceProvider) {
			Message = message;
			Channel = message.Channel;
			Guild = message.Channel.Guild;
			User = (DiscordMember) message.Author;
		}
	}

	public class FridgeCommandModule : ModuleBase<DiscordCommandContext> {
		public FridgeDbContext DbContext { get; set; }

		[Command("init")]
		public async Task<CommandResult> Init(DiscordChannel fridgeChannel) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				DbContext.Servers.Add(new ServerFridge() {
					Id = Context.Guild.Id,
					ChannelId = fridgeChannel.Id
				});
			} else {
				fridge.ChannelId = fridgeChannel.Id;
			}
			return new TextResult("OK", true);
		}

		[Command("emote")]
		public async Task<CommandResult> UpdateEmote(DiscordEmoji emoji, int minimumToAdd, int maximumToRemove) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				return new TextResult("You must use init first", false);
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(emoji.Id);
				if (emote == null) {
					emote = new ServerEmote() {
						EmoteId = emoji.Id
					};
				}

				emote.MinimumToAdd = minimumToAdd;
				emote.MaximumToRemove = maximumToRemove;
				return new TextResult("OK", true);
			}
		}

		[Command("delete")]
		public async Task<CommandResult> UpdateEmote(DiscordEmoji emoji) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				return new TextResult("You must use init first", false);
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(emoji.Id);
				if (emote == null) {
					return new TextResult("Not found", false);
				} else {
					DbContext.Emotes.Remove(emote);
					return new TextResult("OK", true);
				}
			}
		}

		protected async override ValueTask AfterExecutedAsync() {
			await DbContext.SaveChangesAsync();
		}
	}
}