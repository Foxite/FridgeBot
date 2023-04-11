using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Revcord.Entities;

namespace FridgeBot; 

[Index(nameof(ServerId), nameof(ChannelId), nameof(MessageId), IsUnique = true)]
[Index(nameof(ServerId), nameof(FridgeMessageId), IsUnique = true)]
public class FridgeEntry {
	[Key]
	public Guid Id { get; set; }
	public EntityId ServerId { get; set; }
		
	/// <summary>
	///  Id of the channel the message was originally sent in.
	/// </summary>
	public EntityId ChannelId { get; set; }
		
	/// <summary>
	/// Id of the original message.
	/// </summary>
	public EntityId MessageId { get; set; }
		
	/// <summary>
	/// Id of the message sent by the bot in the server fridge.
	/// </summary>
	public EntityId FridgeMessageId { get; set; }
		
	public ServerFridge Server { get; set; }
		
	public ICollection<FridgeEntryEmote> Emotes { get; set; }
}
