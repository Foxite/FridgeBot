using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Qmmands;

namespace FridgeBot {
	public class InfoModule : Qmmands.ModuleBase<DSharpPlusCommandContext> {
		[Command("version")]
		public async Task Version() {
			string? response = Environment.GetEnvironmentVariable("FRIDGEBOT_VERSION");

			if (string.IsNullOrWhiteSpace(response)) {
				response = "Unset";
			}

			await Context.RespondAsync(response);
		}
	}
}
