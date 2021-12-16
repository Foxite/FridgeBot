namespace FridgeBot.Data {
	public class FridgedMessage {
		public ulong Id { get; set; }
		public ulong ChannelId { get; set; }
		public ulong EmoteId { get; set; }
		
		public ulong FridgeMessageId { get; set; }
	}
}