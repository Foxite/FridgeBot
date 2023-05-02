using Revcord.Entities;

namespace FridgeBot;

public record RenderableFridgeMessage(FridgeEntry FridgeEntry, IMessage Message);
