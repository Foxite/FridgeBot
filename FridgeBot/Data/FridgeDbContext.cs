using Microsoft.EntityFrameworkCore;

namespace FridgeBot; 

public class FridgeDbContext : DbContext {
	public DbSet<FridgeEntry> Entries { get; set; }
	//public DbSet<FridgeEntryEmote> EntriesEntryEmotes { get; set; }
	public DbSet<ServerEmote> Emotes { get; set; }
	public DbSet<ServerFridge> Servers { get; set; }

	public FridgeDbContext() : base() { }
	public FridgeDbContext(DbContextOptions<FridgeDbContext> dbco) : base(dbco) { }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
		if (!optionsBuilder.IsConfigured) {
			// For `dotnet ef`
			optionsBuilder.UseNpgsql("Host=database; Port=5432; Username=fridgebot; Password=fridgebot");
		}
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder) {
		base.OnModelCreating(modelBuilder);
		modelBuilder.Entity<ServerEmote>().HasKey(nameof(ServerEmote.ServerId), nameof(ServerEmote.EmoteString));
	}
}