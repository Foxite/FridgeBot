using DSharpPlus;
using DSharpPlus.Entities;
using Qmmands;

namespace FridgeBot;

public class RequireUserPermissionsAttribute : Qmmands.CheckAttribute {
	public Permissions Permissions { get; }

	public RequireUserPermissionsAttribute(Permissions permissions) {
		Permissions = permissions;
	}

	public override ValueTask<CheckResult> CheckAsync(CommandContext context_) {
		if (context_ is not DSharpPlusCommandContext context) {
			return new ValueTask<CheckResult>(CheckResult.Failed("Internal error"));
		}

		if (context.User is not DiscordMember member) {
			return new ValueTask<CheckResult>(CheckResult.Failed("This command can only be executed from a guild"));
		}

		// TODO check for multiple bits in parameter
		if (member.Permissions.HasFlag(Permissions)) {
			return new ValueTask<CheckResult>(CheckResult.Successful);
		} else {
			return new ValueTask<CheckResult>(CheckResult.Failed("You do not have permission"));
		}
	}
}
