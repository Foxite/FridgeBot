using Microsoft.EntityFrameworkCore;
using Qmmands;
using Revcord;
using Revcord.Commands;
using Revcord.Entities;

namespace FridgeBot; 

//[RequireUserPermissions(DSharpPlus.Permissions.ManageGuild)]
public class AdminModule : Qmmands.ModuleBase<RevcordCommandContext> {
	public FridgeDbContext DbContext { get; set; } = null!;

	[Command("init")]
	public async Task Init(IChannel fridgeChannel) {
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
		
		// TODO return results
		await Context.Message.SendReplyAsync("OK");
	}

	[Command("emote")]
	public async Task UpdateEmote(IEmoji emoji, int minimumToAdd, int maximumToRemove) {
		ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild!.Id);
		if (fridge == null) {
			await Context.Message.SendReplyAsync("You must use init first");
		} else {
			ServerEmote? emote = await DbContext.Emotes.FindAsync(Context.Guild.Id, emoji.ToString());
			if (emote == null) {
				emote = new ServerEmote() {
					EmoteString = emoji.ToString(),
					Server = fridge
				};
				DbContext.Emotes.Add(emote);
			}

			emote.MinimumToAdd = minimumToAdd;
			emote.MaximumToRemove = maximumToRemove;
			await Context.Message.SendReplyAsync("OK");
		}
	}

	[Command("status")]
	public async Task GetEmotes() {
		ServerFridge? fridge = await DbContext.Servers.Include(server => server.Emotes).FirstOrDefaultAsync(server => server.Id == Context.Guild!.Id);
		if (fridge == null) {
			await Context.Message.SendReplyAsync("You must use init first");
		} else {
			var channel = await Context.Client.GetChannelAsync(fridge.ChannelId);
			var message = $"Fridge channel: {channel.MentionString}\nEmotes:";

			foreach (ServerEmote emote in fridge.Emotes) {
				message += $"\n- {emote.EmoteString} to add: {emote.MinimumToAdd}; to remove: {emote.MaximumToRemove}";
			}

			await Context.Message.SendReplyAsync(message);
		}
	}

	[Command("delete")]
	public async Task DeleteEmote(IEmoji emoji) {
		ServerFridge? fridge = await DbContext.Servers.FindAsync(Context.Guild!.Id);
		if (fridge == null) {
			await Context.Message.SendReplyAsync("You must use init first");
		} else {
			ServerEmote? emote = await DbContext.Emotes.FindAsync(Context.Guild.Id, emoji.ToString());
			if (emote == null) {
				await Context.Message.SendReplyAsync("Not found");
			} else {
				DbContext.Emotes.Remove(emote);
				await Context.Message.SendReplyAsync("OK");
			}
		}
	}

	protected async override ValueTask AfterExecutedAsync() {
		await DbContext.SaveChangesAsync();
	}
}