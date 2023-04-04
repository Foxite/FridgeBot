using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Qmmands;

namespace FridgeBot; 

[RequireUserPermissions(Permissions.ManageGuild)]
public class AdminModule : Qmmands.ModuleBase<DSharpPlusCommandContext> {
	public FridgeDbContext DbContext { get; set; } = null!;

	[Command("init")]
	public async Task Init(DiscordChannel fridgeChannel) {
		ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild!.Id);
		if (fridge == null) {
			DbContext.Servers.Add(new ServerFridge() {
				Id = Context.Guild.Id,
				ChannelId = fridgeChannel.Id,
				InitializedAt = DateTimeOffset.UtcNow
			});
		} else {
			fridge.ChannelId = fridgeChannel.Id;
		}
		await Context.RespondAsync("OK");
	}

	[Command("emote")]
	public async Task UpdateEmote(DiscordEmoji emoji, int minimumToAdd, int maximumToRemove) {
		ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild!.Id);
		if (fridge == null) {
			await Context.RespondAsync("You must use init first");
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
			await Context.RespondAsync("OK");
		}
	}

	[Command("status")]
	public async Task GetEmotes() {
		ServerFridge? fridge = await DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == Context.Guild!.Id);
		if (fridge == null) {
			await Context.RespondAsync("You must use init first");
		} else {
			var message = $"Fridge channel: <#{fridge.ChannelId}>\nEmotes:";

			foreach (ServerEmote emote in fridge.Emotes) {
				message += $"\n- {emote.EmoteString} to add: {emote.MinimumToAdd}; to remove: {emote.MaximumToRemove}";
			}

			await Context.RespondAsync(message);
		}
	}

	[Command("delete")]
	public async Task DeleteEmote(DiscordEmoji emoji) {
		ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild!.Id);
		if (fridge == null) {
			await Context.RespondAsync("You must use init first");
		} else {
			ServerEmote? emote = await DbContext.Emotes.FindAsync(Context.Guild.Id, emoji.ToStringInvariant());
			if (emote == null) {
				await Context.RespondAsync("Not found");
			} else {
				DbContext.Emotes.Remove(emote);
				await Context.RespondAsync("OK");
			}
		}
	}

	protected async override ValueTask AfterExecutedAsync() {
		await DbContext.SaveChangesAsync();
	}
}