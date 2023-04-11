/*
// TODO reimplement
// TODO try to move this into Revcord
using Qmmands;
using Revcord.Commands;

namespace FridgeBot;

public class RequireUserPermissionsAttribute : Qmmands.CheckAttribute {
	public Permissions Permissions { get; }

	public RequireUserPermissionsAttribute(Permissions permissions) {
		Permissions = permissions;
	}

	public override ValueTask<CheckResult> CheckAsync(CommandContext context_) {
		if (context_ is not RevcordCommandContext context) {
			return new ValueTask<CheckResult>(CheckResult.Failed("Internal error"));
		}

		if (context.Member == null) {
			return new ValueTask<CheckResult>(CheckResult.Failed("This command can only be executed from a guild"));
		}

		// TODO check for multiple bits in parameter
		if (context.Member.Permissions.HasFlag(Permissions)) {
			return new ValueTask<CheckResult>(CheckResult.Successful);
		} else {
			return new ValueTask<CheckResult>(CheckResult.Failed("You do not have permission"));
		}
	}
}
*/
