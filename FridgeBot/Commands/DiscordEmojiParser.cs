using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot;

public class DiscordEmojiParser : TypeParser<DiscordEmoji> {
	public static readonly Regex Regex = new Regex(@"^<a?:\w+:(?<Id>[0-9]+)>$");
		
	public override ValueTask<TypeParserResult<DiscordEmoji>> ParseAsync(Parameter parameter, string value, CommandContext context_) {
		if (context_ is not DSharpPlusCommandContext context) {
			return TypeParserResult<DiscordEmoji>.Failed("Internal error");
		}

		DiscordEmoji? result = null;
		Match match = Regex.Match(value);
		if (match.Success) {
			ulong id = ulong.Parse(match.Groups["Id"].Value);
			try {
				result = DiscordEmoji.FromGuildEmote(context.Client, id);
			} catch (KeyNotFoundException) {
				result = null;
			}
		} else {
			try {
				result = DiscordEmoji.FromUnicode(value);
			} catch (ArgumentException) {
				result = null;
			}
		}
			
		if (result != null) {
			return TypeParserResult<DiscordEmoji>.Successful(result);
		} else {
			return TypeParserResult<DiscordEmoji>.Failed("Not a emote or emoji");
		}
	}
}
