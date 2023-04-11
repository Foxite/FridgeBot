using System.ComponentModel.DataAnnotations;
using Revcord.Entities;

namespace FridgeBot; 

public class ServerFridge {
	/// <summary>
	/// Id of the server.
	/// </summary>
	[Key]
	public EntityId Id { get; set; }
		
	/// <summary>
	/// Id of the channel to send fridged messages.
	/// </summary>
	public EntityId ChannelId { get; set; }
		
	public DateTimeOffset InitializedAt { get; set; } = DateTimeOffset.MinValue;
		
	public ICollection<FridgeEntry> FridgeEntries { get; set; }
	public ICollection<ServerEmote> Emotes { get; set; }
		
	public ServerFridge() {}

	public ServerFridge(EntityId id, EntityId channelId, DateTimeOffset initializedAt) {
		Id = id;
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
