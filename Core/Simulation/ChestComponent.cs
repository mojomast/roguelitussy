using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class ChestComponent
{
    public string LootTableId { get; set; } = "floor_loot";

    public bool HasRolled { get; set; }

    public List<ItemInstance> Contents { get; } = new();
}
