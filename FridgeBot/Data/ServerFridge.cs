using System.ComponentModel.DataAnnotations;

namespace FridgeBot {
	public class ServerFridge {
		[Key]
		public ulong Id { get; set; }
		
		/// <summary>
		/// Id of the channel to send fridged messages.
		/// </summary>
		public ulong ChannelId { get; set; }
		
		public ICollection<FridgeEntry> FridgeEntries { get; set; }
		public ICollection<ServerEmote> Emotes { get; set; }
	}
}
