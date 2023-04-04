using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot;

public class DiscordChannelParser : TypeParser<DiscordChannel> {
	public static readonly Regex Regex = new Regex(@"^<#(?<Id>[0-9]+)>$");
		
	public async override ValueTask<TypeParserResult<DiscordChannel>> ParseAsync(Parameter parameter, string value, CommandContext context_) {
		if (context_ is not DSharpPlusCommandContext context) {
			return TypeParserResult<DiscordChannel>.Failed("Internal error");
		}
			
		ulong? id = null;
		if (ulong.TryParse(value, out ulong parsedId)) {
			id = parsedId;
		} else {
			Match match = Regex.Match(value);
			if (match.Success) {
				id = ulong.Parse(match.Groups["Id"].Value);
			}
		}

		if (id.HasValue) {
			return TypeParserResult<DiscordChannel>.Successful(await context.Client.GetChannelAsync(id.Value));
		} else {
			return TypeParserResult<DiscordChannel>.Failed("Not a valid channel mention or id");
		}
	}
}
