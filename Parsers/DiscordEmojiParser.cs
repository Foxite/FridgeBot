using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace FridgeBot {
	public class DiscordEmojiParser : TypeParser<DiscordEmoji> {
		private static bool TryGetIdFromGuildEmote(string emote, out ulong id) {
			// based on code borrowed (tm) from discord.net
			if (emote.StartsWith("<:") || emote.StartsWith("<a:") && emote.EndsWith(">")) {
				int indexOfSecondColon = emote.IndexOf(':', 3);
				if (indexOfSecondColon != -1) {
					return ulong.TryParse(emote[(indexOfSecondColon + 1)..^1], out id);
				}
			}

			id = 0;
			return false;
		}
		
		public override ValueTask<TypeParserResult<DiscordEmoji>> ParseAsync(Parameter parameter, string input, CommandContext context) {
			if (DiscordEmoji.TryFromUnicode(input, out DiscordEmoji emoji) || (TryGetIdFromGuildEmote(input, out ulong emoteId) && DiscordEmoji.TryFromGuildEmote(context.Services.GetRequiredService<DiscordClient>(), emoteId, out emoji))) {
				return new ValueTask<TypeParserResult<DiscordEmoji>>(TypeParserResult<DiscordEmoji>.Successful(emoji));
			} else {
				return new ValueTask<TypeParserResult<DiscordEmoji>>(TypeParserResult<DiscordEmoji>.Failed("Not an emoji or emote"));
			}
		}
	}
}