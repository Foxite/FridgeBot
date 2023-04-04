using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot; 

[Index(nameof(FridgeEntryId), nameof(EmoteString), IsUnique = true)]
public class FridgeEntryEmote {
	[Key] public Guid Id { get; set; }
	public Guid FridgeEntryId { get; set; }
		
	public string EmoteString { get; set; }
		
	public FridgeEntry FridgeEntry { get; set; }
}