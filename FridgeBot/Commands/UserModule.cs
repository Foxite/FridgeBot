// TODO: stats commands
// We need to start tracking this information in the database:
// - EntryEmote: counts, who gave the emotes (by user ID and able to quickly get the sums of every user ID)
// - FridgeEntry: channel, user
// We also need to add a command that populates all of this information for existing servers.

/*
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot; 

public class UserModule : BaseCommandModule {
	public FridgeDbContext DbContext { get; set; } = null!;

	[Command("stats")]
	public async Task GuildStats(CommandContext ctx) {
		DateTimeOffset initDate = (await DbContext.Servers.FindAsync(ctx.Guild.Id)).InitializedAt;
		var stats = await DbContext.Servers.Where(sf => sf.Id == ctx.Guild.Id).Select(sf => new {
			EntryCount = sf.FridgeEntries.Count,
			EntryEmoteSum = ,
			TopEntries = ,
			TopReceivers = , // by emote
			TopGivers = ,
		}).SingleAsync();
	}

	[Command("stats")]
	public async Task UserStats(CommandContext ctx, DiscordUser user) {
		DateTimeOffset initDate = (await DbContext.Servers.FindAsync(ctx.Guild.Id)).InitializedAt;
		var stats = await DbContext.Servers.Where(sf => sf.Id == ctx.Guild.Id).Select(sf => new {
			EntryCount = sf.FridgeEntries.Count,
			TopEntries = ,
			Received = ,
			Given = ,
		}).SingleAsync();
	}

	[Command("stats")]
	public async Task ChannelStats(CommandContext ctx, DiscordChannel channel) {
		DateTimeOffset initDate = (await DbContext.Servers.FindAsync(ctx.Guild.Id)).InitializedAt;
		var stats = await DbContext.Servers.Where(sf => sf.Id == ctx.Guild.Id).Select(sf => new {
			EntryCount = sf.FridgeEntries.Count,
			EntryEmoteSum = ,
			TopEntries = ,
			TopReceivers = ,
			TopGivers = ,
		}).SingleAsync();
	}
}
//*/
