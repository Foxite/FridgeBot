using Microsoft.EntityFrameworkCore;

namespace FridgeBot.Data {
	public class FridgeDbContext : DbContext {
		public DbSet<FridgedMessage> Messages { get; set; }
		public DbSet<FridgeEmote> Emotes { get; set; }
	}
}