using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class CharacterOptionsSaveData
{
    public string Name { get; set; } = "Rook";

    public string Archetype { get; set; } = "Vanguard";

    public string Origin { get; set; } = "Survivor";

    public string Trait { get; set; } = "Iron Will";

    public string RaceId { get; set; } = "human";

    public string GenderId { get; set; } = "neutral";

    public string AppearanceId { get; set; } = "default";

    public int BonusMaxHp { get; set; }

    public int BonusAttack { get; set; }

    public int BonusDefense { get; set; }

    public int BonusAccuracy { get; set; }

    public int BonusEvasion { get; set; }

    public int BonusSpeed { get; set; }

    public int BonusViewRadius { get; set; }

    public int InventoryCapacityBonus { get; set; }

    public List<string> StartingItemTemplateIds { get; set; } = new();

    public List<string> EquippedItemTemplateIds { get; set; } = new();
}
