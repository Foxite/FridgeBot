using System;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FridgeBot {
	public static class Util {
		/// <summary>
		/// Get the string used to store the emoji in the database as a ServerEmote.
		/// 
		/// Note: when the DiscordEmoji object comes from MessageReactionRemoved, IsAnimated will always be false, causing ToString to start with &lt;a: rather than &lt;:, but every other case will work properly.
		/// This method is used to "normalize" the strings so that they never start with &lt;a: and always with &lt;:.
		/// </summary>
		public static string ToStringInvariant(this DiscordEmoji emoji) {
			return emoji.ToString().Replace("<a:", "<:");
		}
	}
}
