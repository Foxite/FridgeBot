using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot {
	[Index(nameof(FridgeEntryId), nameof(EmoteId), IsUnique = true)]
	public class FridgeEntryEmote {
		[Key] public Guid Id { get; set; }
		public Guid FridgeEntryId { get; set; }
		public ulong EmoteId { get; set; }

		public FridgeEntry FridgeEntry { get; set; }
	}
}
