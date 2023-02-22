using System.ComponentModel.DataAnnotations;

namespace FridgeBot {
	public class ServerFridge {
		[Key]
		public ulong Id { get; set; }
		
		/// <summary>
		/// Id of the channel to send fridged messages.
		/// </summary>
		public ulong ChannelId { get; set; }
		
		public DateTimeOffset InitializedAt { get; set; } = DateTimeOffset.MinValue;
		
		public ICollection<FridgeEntry> FridgeEntries { get; set; }
		public ICollection<ServerEmote> Emotes { get; set; }
		
		public ServerFridge() {}

		public ServerFridge(ulong channelId, DateTimeOffset initializedAt) {
			ChannelId = channelId;
			InitializedAt = initializedAt;
			FridgeEntries = new List<FridgeEntry>();
			Emotes = new List<ServerEmote>();
		}

		public ServerFridge AddEmote(ServerEmote emote) {
			emote.ServerId = Id;
			emote.Server = this;
			Emotes.Add(emote);
			return this;
		}
	}
}
