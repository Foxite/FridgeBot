using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace FridgeBot {
	public class ChannelParser : TypeParser<DiscordChannel> {
		public async override ValueTask<TypeParserResult<DiscordChannel>> ParseAsync(Parameter parameter, string input, CommandContext context) {
			// TODO channel mention
			if (ulong.TryParse(input, out ulong channelId)) {
				DiscordChannel? channel = await context.Services.GetRequiredService<DiscordClient>().GetChannelAsync(channelId);
				if (channel == null) {
					return TypeParserResult<DiscordChannel>.Failed("Unknown channel");
				} else {
					return TypeParserResult<DiscordChannel>.Successful(channel);
				}
			} else {
				return TypeParserResult<DiscordChannel>.Failed("Invalid ID or mention");
			}
		}
	}
}
