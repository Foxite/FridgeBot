using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot {
	[RequireUserPermissions(Permissions.ManageGuild)]
	public class AdminModule : BaseCommandModule {
		public FridgeDbContext DbContext { get; set; } = null!;

		[Command("init")]
		public async Task Init(CommandContext context, DiscordChannel fridgeChannel) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(context.Guild.Id);
			if (fridge == null) {
				DbContext.Servers.Add(new ServerFridge() {
					Id = context.Guild.Id,
					ChannelId = fridgeChannel.Id,
					InitializedAt = DateTimeOffset.UtcNow
				});
			} else {
				fridge.ChannelId = fridgeChannel.Id;
			}
			await context.RespondAsync("OK");
		}

		[Command("emote")]
		public async Task UpdateEmote(CommandContext context, DiscordEmoji emoji, int minimumToAdd, int maximumToRemove) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(context.Guild.Id);
			if (fridge == null) {
				await context.RespondAsync("You must use init first");
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(context.Guild.Id, emoji.ToStringInvariant());
				if (emote == null) {
					emote = new ServerEmote() {
						EmoteString = emoji.ToStringInvariant(),
						Server = fridge
					};
					DbContext.Emotes.Add(emote);
				}

				emote.MinimumToAdd = minimumToAdd;
				emote.MaximumToRemove = maximumToRemove;
				await context.RespondAsync("OK");
			}
		}

		[Command("status")]
		public async Task GetEmotes(CommandContext context) {
			ServerFridge? fridge = await DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == context.Guild.Id);
			if (fridge == null) {
				await context.RespondAsync("You must use init first");
			} else {
				var message = $"Fridge channel: <#{fridge.ChannelId}>\nEmotes:";

				foreach (ServerEmote emote in fridge.Emotes) {
					message += $"\n- {emote.EmoteString} to add: {emote.MinimumToAdd}; to remove: {emote.MaximumToRemove}";
				}

				await context.RespondAsync(message);
			}
		}

		[Command("delete")]
		public async Task DeleteEmote(CommandContext context, DiscordEmoji emoji) {
			ServerFridge? fridge = await DbContext.Servers.FindAsync(context.Guild.Id);
			if (fridge == null) {
				await context.RespondAsync("You must use init first");
			} else {
				ServerEmote? emote = await DbContext.Emotes.FindAsync(context.Guild.Id, emoji.ToStringInvariant());
				if (emote == null) {
					await context.RespondAsync("Not found");
				} else {
					DbContext.Emotes.Remove(emote);
					await context.RespondAsync("OK");
				}
			}
		}

		public override Task AfterExecutionAsync(CommandContext ctx) {
			return DbContext.SaveChangesAsync();
		}
	}
}
