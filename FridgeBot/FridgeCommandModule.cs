using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot {
	public class FridgeCommandModule : ModuleBase<DiscordCommandContext> {
		public FridgeDbContext DbContext { get; set; }

		[Command("init")]
		public async Task<CommandResult> Init(DiscordChannel fridgeChannel) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				DbContext.Servers.Add(new ServerFridge() {
					Id = Context.Guild.Id,
					ChannelId = fridgeChannel.Id,
					InitializedAt = DateTimeOffset.UtcNow
				});
			} else {
				fridge.ChannelId = fridgeChannel.Id;
			}
			return new TextResult("OK", true);
		}

		[Command("initdate")]
		public async Task<CommandResult> Init(ulong idOfInitCommand) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				return new TextResult("You must use init first", false);
			} else {
				fridge.InitializedAt = DSharpPlus.Utilities.GetSnowflakeTime(idOfInitCommand);
				return new TextResult("OK", true);
			}
		}

		[Command("emote")]
		public async Task<CommandResult> UpdateEmote(DiscordEmoji emoji, int minimumToAdd, int maximumToRemove) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				return new TextResult("You must use init first", false);
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(Context.Guild.Id, emoji.ToStringInvariant());
				if (emote == null) {
					emote = new ServerEmote() {
						EmoteString = emoji.ToStringInvariant(),
						Server = fridge
					};
					DbContext.Emotes.Add(emote);
				}

				emote.MinimumToAdd = minimumToAdd;
				emote.MaximumToRemove = maximumToRemove;
				return new TextResult("OK", true);
			}
		}

		[Command("delete")]
		public async Task<CommandResult> DeleteEmote(DiscordEmoji emoji) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild.Id);
			if (fridge == null) {
				return new TextResult("You must use init first", false);
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(Context.Guild.Id, emoji.ToStringInvariant());
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
