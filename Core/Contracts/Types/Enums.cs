namespace Roguelike.Core;

public enum TileType : byte
{
    Void = 0,
    Wall = 1,
    Floor = 2,
    Door = 3,
    StairsDown = 4,
    StairsUp = 5,
    Water = 6,
    Lava = 7,
}

public enum ActionType : byte
{
    Wait,
    Move,
    MeleeAttack,
    RangedAttack,
    UseItem,
    PickupItem,
    DropItem,
    OpenDoor,
    CloseDoor,
    Descend,
    Ascend,
    CastAbility,
}

public enum ActionResult : byte
{
    Success,
    Invalid,
    Blocked,
}

public enum DamageType : byte
{
    Physical,
    Fire,
    Cold,
    Poison,
    Lightning,
    Holy,
    Dark,
}

public enum Faction : byte
{
    Player,
    Enemy,
    Neutral,
}

public enum StatusEffectType : byte
{
    None,
    Poisoned,
    Burning,
    Frozen,
    Stunned,
    Hasted,
    Invisible,
    Regenerating,
    Weakened,
    Shielded,
}

public enum EquipSlot : byte
{
    None,
    MainHand,
    OffHand,
    Head,
    Body,
    Feet,
    Ring,
    Amulet,
}

public enum ItemCategory : byte
{
    Weapon,
    Armor,
    Consumable,
    Scroll,
    Key,
    Misc,
}
