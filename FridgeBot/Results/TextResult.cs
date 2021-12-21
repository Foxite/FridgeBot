using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace FridgeBot {
	public class TextResult : CommandResult {
		public string Response { get; }

		public override bool IsSuccessful { get; }

		public TextResult(string response, bool isSuccessful) {
			Response = response;
			IsSuccessful = isSuccessful;
		}

		public override string ToString() => Response;
	}
}
