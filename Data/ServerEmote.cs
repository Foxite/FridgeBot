using System.ComponentModel.DataAnnotations;

namespace FridgeBot {
	public class ServerEmote {
		//[Key] // Composite key specified in dbcontext model builder
		public ulong EmoteId { get; set; }
		//[Key]
		public ulong ServerId { get; set; }

		public ServerFridge Server { get; set; }
		
		/// <summary>
		/// When the emote count is equal or greater than this, the message gets added to the fridge if it is not already present.
		/// </summary>
		public int MinimumToAdd { get; set; }
		
		/// <summary>
		/// When the emote count is less than this, the message gets removed from the fridge if it is present.
		/// </summary>
		public int MaximumToRemove { get; set; }
	}
}