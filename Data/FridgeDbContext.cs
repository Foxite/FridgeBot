using Microsoft.EntityFrameworkCore;

namespace FridgeBot {
	public class FridgeDbContext : DbContext {
		public DbSet<FridgeEntry> Entries { get; set; }
		//public DbSet<FridgeEntryEmote> EntriesEntryEmotes { get; set; }
		public DbSet<ServerEmote> Emotes { get; set; }
		public DbSet<ServerFridge> Servers { get; set; }

		public FridgeDbContext(DbContextOptions<FridgeDbContext> dbco) : base(dbco) { }
	}
}