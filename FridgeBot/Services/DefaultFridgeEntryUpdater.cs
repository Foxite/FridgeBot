namespace FridgeBot;

// TODO unit test this separately
public class DefaultFridgeEntryUpdater : IFridgeEntryUpdater {
	public Task UpdateFridgeEntry(FridgeEntry entry, IDiscordMessage message) {
		foreach (FridgeEntryEmote removeEmote in
				from entryEmote in entry.Emotes
				let reaction = message.Reactions.FirstOrDefault(reaction => reaction.Emoji.ToStringInvariant() == entryEmote.EmoteString)
				where reaction == null || reaction.Count < entry.Server.Emotes.First(serverEmote => serverEmote.EmoteString == entryEmote.EmoteString).MaximumToRemove
				let item = entry.Emotes.FirstOrDefault(emote => emote.EmoteString == entryEmote.EmoteString)
				where item != null
				select item) {
			entry.Emotes.Remove(removeEmote);
		}
			
		foreach (FridgeEntryEmote addEmote in
				from reaction in message.Reactions
				let serverEmote = entry.Server.Emotes.FirstOrDefault(emote => emote.EmoteString == reaction.Emoji.ToStringInvariant())
				where serverEmote != null && reaction.Count >= serverEmote.MinimumToAdd && !entry.Emotes.Any(entryEmote => entryEmote.EmoteString == serverEmote.EmoteString)
				let entryEmote = new FridgeEntryEmote { EmoteString = reaction.Emoji.ToStringInvariant() }
				select entryEmote) {
			entry.Emotes.Add(addEmote);
		}

		return Task.CompletedTask;
	}
}
