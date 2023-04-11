using Revcord.Entities;

namespace FridgeBot; 

public class ServerEmote {
	//[Key] // Composite key specified in dbcontext model builder
	public EntityId ServerId { get; set; }
	// For guild emotes, this is the string in the form of <:name:0123456789:> or <a:name:0123456789:>.
	// For emojis this is the unicode for the emoji.
	//[Key]
	public string EmoteString { get; set; }

	public ServerFridge Server { get; set; }
		
	/// <summary>
	/// When the emote count is equal or greater than this, the message gets added to the fridge if it is not already present.
	/// </summary>
	public int MinimumToAdd { get; set; }
		
	/// <summary>
	/// When the emote count is less than this, the message gets removed from the fridge if it is present.
	/// </summary>
	public int MaximumToRemove { get; set; }

	public ServerEmote() {}
	public ServerEmote(string emoteString, int minimumToAdd, int maximumToRemove) {
		EmoteString = emoteString;
		MinimumToAdd = minimumToAdd;
		MaximumToRemove = maximumToRemove;
	}
}
