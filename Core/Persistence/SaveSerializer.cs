using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelike.Core;

internal sealed class SaveFileData
{
    public int Version { get; set; }

    public DateTime SavedAt { get; set; }

    public int? ContentVersion { get; set; }

    public string? ContentHash { get; set; }

    public int Seed { get; set; }

    public int Depth { get; set; }

    public int TurnNumber { get; set; }

    public ulong CombatRandomState { get; set; }

    public ulong ItemRandomState { get; set; }

    public int SchedulerNextOrder { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string Tiles { get; set; } = string.Empty;

    public string Explored { get; set; } = string.Empty;

    public string Visible { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

    public List<EntitySaveData> Entities { get; set; } = new();

    public List<GroundItemSaveData> GroundItems { get; set; } = new();

    public List<PositionSaveData> OpenDoors { get; set; } = new();

    public List<FloorSaveData> Floors { get; set; } = new();

    public CharacterOptionsSaveData CharacterOptions { get; set; } = new();
}

internal sealed class FloorSaveData
{
    public int Depth { get; set; }

    public int TurnNumber { get; set; }

    public ulong CombatRandomState { get; set; }

    public ulong ItemRandomState { get; set; }

    public int SchedulerNextOrder { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string Tiles { get; set; } = string.Empty;

    public string Explored { get; set; } = string.Empty;

    public string Visible { get; set; } = string.Empty;

    public List<EntitySaveData> Entities { get; set; } = new();

    public List<GroundItemSaveData> GroundItems { get; set; } = new();

    public List<PositionSaveData> OpenDoors { get; set; } = new();
}

internal sealed class EntitySaveData
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public PositionSaveData Position { get; set; } = new();

    public int Faction { get; set; }

    public bool BlocksMovement { get; set; }

    public bool BlocksSight { get; set; }

    public StatsSaveData Stats { get; set; } = new();

    public InventorySaveData? Inventory { get; set; }

    public StatusEffectsSaveData? StatusEffects { get; set; }

    public ProgressionSaveData? Progression { get; set; }

    public IdentitySaveData? Identity { get; set; }

    public WalletSaveData? Wallet { get; set; }

    public NpcSaveData? Npc { get; set; }

    public MerchantSaveData? Merchant { get; set; }

    public ChestSaveData? Chest { get; set; }

    public XpValueSaveData? XpValue { get; set; }

    public AbilitiesSaveData? Abilities { get; set; }

    public CooldownsSaveData? Cooldowns { get; set; }

    public AIStateSaveData? AIState { get; set; }

    public string BrainType { get; set; } = string.Empty;

    public EnemySaveData? Enemy { get; set; }

    public TrapSaveData? Trap { get; set; }

    public int SchedulerOrder { get; set; }
}

internal sealed class EnemySaveData
{
    public string TemplateId { get; set; } = string.Empty;
}

internal sealed class ChestSaveData
{
    public string LootTableId { get; set; } = string.Empty;
}

internal sealed class TrapSaveData
{
    public string TemplateId { get; set; } = string.Empty;

    public bool IsArmed { get; set; } = true;

    public bool IsRevealed { get; set; }

    public int TriggerCount { get; set; }
}

internal sealed class XpValueSaveData
{
    public int Value { get; set; }
}

internal sealed class AbilitiesSaveData
{
    public List<AbilitySlotSaveData> Slots { get; set; } = new();
}

internal sealed class AbilitySlotSaveData
{
    public string AbilityId { get; set; } = string.Empty;
    public int Cooldown { get; set; }
    public int Priority { get; set; }
}

internal sealed class CooldownsSaveData
{
    public Dictionary<string, int> Active { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class AIStateSaveData
{
    public int State { get; set; }
    public int IdleTurns { get; set; }
    public PositionSaveData PatrolTarget { get; set; } = new();
    public int PatrolSteps { get; set; }
    public int PatrolSequence { get; set; }
    public PositionSaveData LastKnownTargetPosition { get; set; } = new();
    public string TargetId { get; set; } = string.Empty;
}

internal sealed class WalletSaveData
{
    public int Gold { get; set; }
}

internal sealed class NpcSaveData
{
    public string TemplateId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string DialogueId { get; set; } = string.Empty;
}

internal sealed class MerchantSaveData
{
    public List<MerchantOfferSaveData> Offers { get; set; } = new();
}

internal sealed class MerchantOfferSaveData
{
    public string ItemTemplateId { get; set; } = string.Empty;

    public int Price { get; set; }

    public int Quantity { get; set; }
}

internal sealed class ProgressionSaveData
{
    public int Level { get; set; }

    public int Experience { get; set; }

    public int ExperienceToNextLevel { get; set; }

    public int UnspentStatPoints { get; set; }

    public int UnspentPerkChoices { get; set; }

    public int Kills { get; set; }

    public List<string> SelectedPerkIds { get; set; } = new();
}

internal sealed class IdentitySaveData
{
    public string RaceId { get; set; } = string.Empty;

    public string GenderId { get; set; } = string.Empty;

    public string AppearanceId { get; set; } = string.Empty;

    public string SpriteVariantId { get; set; } = string.Empty;
}

internal sealed class StatsSaveData
{
    public int HP { get; set; }

    public int MaxHP { get; set; }

    public int Attack { get; set; }

    public int Accuracy { get; set; }

    public int Defense { get; set; }

    public int Evasion { get; set; }

    public int Speed { get; set; }

    public int ViewRadius { get; set; }

    public int Energy { get; set; }
}

internal sealed class InventorySaveData
{
    public int Capacity { get; set; }

    public List<ItemSaveData> Items { get; set; } = new();

    public List<EquippedItemSaveData> Equipped { get; set; } = new();
}

internal sealed class EquippedItemSaveData
{
    public string ItemId { get; set; } = string.Empty;

    public int Slot { get; set; }

    public Dictionary<string, int> StatModifiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class StatusEffectsSaveData
{
    public List<StatusEffectSaveData> Effects { get; set; } = new();
}

internal sealed class StatusEffectSaveData
{
    public int Type { get; set; }

    public int RemainingTurns { get; set; }

    public int Magnitude { get; set; }

    public string SourceEntityId { get; set; } = string.Empty;
}

internal sealed class ItemSaveData
{
    public string InstanceId { get; set; } = string.Empty;

    public string TemplateId { get; set; } = string.Empty;

    public int CurrentCharges { get; set; }

    public int StackCount { get; set; }

    public bool IsIdentified { get; set; }
}

internal sealed class GroundItemSaveData
{
    public PositionSaveData Position { get; set; } = new();

    public ItemSaveData Item { get; set; } = new();
}

internal sealed class PositionSaveData
{
    public int X { get; set; }

    public int Y { get; set; }
}

public static class SaveSerializer
{
    public const int CurrentVersion = 12;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    internal static SaveFileData CreateSaveData(WorldState world, DateTime savedAt, CharacterOptionsSaveData? characterOptions = null)
    {
        var floor = CreateFloorSaveData(world);
        var data = new SaveFileData
        {
            Version = CurrentVersion,
            SavedAt = savedAt,
            ContentVersion = world.ContentDatabase?.ContentVersion,
            ContentHash = world.ContentDatabase?.ContentHash,
            Seed = world.Seed,
            Depth = world.Depth,
            TurnNumber = world.TurnNumber,
            PlayerId = world.Player.Id.Value.ToString("N"),
            Floors = new List<FloorSaveData> { floor },
            CharacterOptions = characterOptions ?? new CharacterOptionsSaveData(),
        };

        ApplyActiveFloorAliases(data, floor);
        return data;
    }

    internal static SaveFileData CreateSaveData(SaveRunSnapshot snapshot, DateTime savedAt)
    {
        var floors = snapshot.Floors
            .Values
            .Concat(new[] { snapshot.ActiveWorld })
            .GroupBy(world => world.Depth)
            .Select(group => group.Last())
            .OrderBy(world => world.Depth)
            .Select(CreateFloorSaveData)
            .ToList();
        var activeFloor = floors.FirstOrDefault(floor => floor.Depth == snapshot.CurrentFloor)
            ?? CreateFloorSaveData(snapshot.ActiveWorld);
        if (!floors.Any(floor => floor.Depth == activeFloor.Depth))
        {
            floors.Add(activeFloor);
            floors.Sort((left, right) => left.Depth.CompareTo(right.Depth));
        }

        var data = new SaveFileData
        {
            Version = CurrentVersion,
            SavedAt = savedAt,
            ContentVersion = snapshot.ActiveWorld.ContentDatabase?.ContentVersion,
            ContentHash = snapshot.ActiveWorld.ContentDatabase?.ContentHash,
            Seed = snapshot.Seed,
            Depth = snapshot.CurrentFloor,
            TurnNumber = snapshot.ActiveWorld.TurnNumber,
            PlayerId = snapshot.ActiveWorld.Player.Id.Value.ToString("N"),
            Floors = floors,
            CharacterOptions = snapshot.CharacterOptions,
        };

        ApplyActiveFloorAliases(data, activeFloor);
        return data;
    }

    public static string ToJson(WorldState world, DateTime savedAt)
    {
        var data = CreateSaveData(world, savedAt);
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    internal static string ToJson(SaveFileData data) => JsonSerializer.Serialize(data, JsonOptions);

    internal static SaveMetadata ToMetadata(SaveFileData data, int slotIndex)
    {
        var playerName = data.Entities.First(entity => string.Equals(entity.Id, data.PlayerId, StringComparison.OrdinalIgnoreCase)).Name;
        return new SaveMetadata(slotIndex, data.Depth, data.TurnNumber, playerName, data.SavedAt, data.Version, data.ContentVersion, data.ContentHash);
    }

    internal static WorldState ToWorldState(SaveFileData data) => ToWorldState(data, null);

    internal static WorldState ToWorldState(SaveFileData data, IContentDatabase? content)
    {
        var floor = data.Floors.FirstOrDefault(candidate => candidate.Depth == data.Depth);
        if (floor is not null)
        {
            return ToWorldState(data, floor, requirePlayer: true, content);
        }

        return ToWorldState(data, CreateFloorFromRoot(data), requirePlayer: true, content);
    }

    internal static SaveRunSnapshot ToRunSnapshot(SaveFileData data) => ToRunSnapshot(data, null);

    internal static SaveRunSnapshot ToRunSnapshot(SaveFileData data, IContentDatabase? content)
    {
        var floorData = data.Floors.Count == 0
            ? new List<FloorSaveData> { CreateFloorFromRoot(data) }
            : data.Floors;
        var floors = floorData.ToDictionary(
            floor => floor.Depth,
            floor => ToWorldState(data, floor, requirePlayer: floor.Depth == data.Depth, content),
            EqualityComparer<int>.Default);
        var activeWorld = floors.TryGetValue(data.Depth, out var active)
            ? active
            : throw new InvalidDataException("Active floor missing from save data.");

        return new SaveRunSnapshot(data.Seed, data.Depth, activeWorld, floors, data.CharacterOptions ?? new CharacterOptionsSaveData());
    }

    private static WorldState ToWorldState(SaveFileData data, FloorSaveData floor, bool requirePlayer, IContentDatabase? content = null)
    {
        var world = new WorldState();
        world.InitGrid(floor.Width, floor.Height);
        world.RehydrateRandomStates(data.Seed, floor.CombatRandomState, floor.ItemRandomState);

        world.Depth = floor.Depth;
        world.TurnNumber = floor.TurnNumber;
        world.SchedulerNextOrder = floor.SchedulerNextOrder;

        var tiles = DecodeTiles(floor.Tiles, floor.Width, floor.Height);
        for (var index = 0; index < tiles.Length; index++)
        {
            world.SetTile(IndexToPosition(index, floor.Width), tiles[index]);
        }

        var explored = DecodeFlags(floor.Explored, floor.Width, floor.Height);
        for (var index = 0; index < explored.Length; index++)
        {
            if (explored[index])
            {
                world.SetVisible(IndexToPosition(index, floor.Width), true);
            }
        }

        world.ClearVisibility();

        var visible = DecodeFlags(floor.Visible, floor.Width, floor.Height);
        for (var index = 0; index < visible.Length; index++)
        {
            if (visible[index])
            {
                world.SetVisible(IndexToPosition(index, floor.Width), true);
            }
        }

        foreach (var door in floor.OpenDoors)
        {
            world.SetDoorOpen(new Position(door.X, door.Y), true);
        }

        foreach (var entityData in floor.Entities)
        {
            world.AddEntity(ToEntity(entityData, content));
        }

        foreach (var entityData in floor.Entities)
        {
            if (entityData.SchedulerOrder != 0)
            {
                world.SchedulerOrders[EntityId.From(entityData.Id)] = entityData.SchedulerOrder;
            }
        }

        var playerId = EntityId.From(data.PlayerId);
        var player = world.GetEntity(playerId);
        if (player is not null)
        {
            world.Player = player;
        }
        else if (requirePlayer)
        {
            throw new InvalidDataException("Player entity missing from save data.");
        }

        foreach (var groundItem in floor.GroundItems)
        {
            world.DropItem(new Position(groundItem.Position.X, groundItem.Position.Y), ToItemInstance(groundItem.Item));
        }

        return world;
    }

    internal static FloorSaveData CreateFloorFromRoot(SaveFileData data) => new()
    {
        Depth = data.Depth,
        TurnNumber = data.TurnNumber,
        CombatRandomState = data.CombatRandomState,
        ItemRandomState = data.ItemRandomState,
        SchedulerNextOrder = data.SchedulerNextOrder,
        Width = data.Width,
        Height = data.Height,
        Tiles = data.Tiles,
        Explored = data.Explored,
        Visible = data.Visible,
        Entities = data.Entities,
        GroundItems = data.GroundItems,
        OpenDoors = data.OpenDoors,
    };

    internal static void ApplyActiveFloorAliases(SaveFileData data, FloorSaveData floor)
    {
        data.TurnNumber = floor.TurnNumber;
        data.CombatRandomState = floor.CombatRandomState;
        data.ItemRandomState = floor.ItemRandomState;
        data.SchedulerNextOrder = floor.SchedulerNextOrder;
        data.Width = floor.Width;
        data.Height = floor.Height;
        data.Tiles = floor.Tiles;
        data.Explored = floor.Explored;
        data.Visible = floor.Visible;
        data.Entities = floor.Entities;
        data.GroundItems = floor.GroundItems;
        data.OpenDoors = floor.OpenDoors;
    }

    private static FloorSaveData CreateFloorSaveData(WorldState world) => new()
    {
        Depth = world.Depth,
        TurnNumber = world.TurnNumber,
        CombatRandomState = world.CombatRandomState,
        ItemRandomState = world.ItemRandomState,
        SchedulerNextOrder = world.SchedulerNextOrder,
        Width = world.Width,
        Height = world.Height,
        Tiles = EncodeTiles(world.GetRawGrid()),
        Explored = EncodeFlags(world.GetRawExplored()),
        Visible = EncodeFlags(ReadVisible(world)),
        Entities = world.Entities.Select(entity => ToSaveData(entity, world)).ToList(),
        GroundItems = FlattenGroundItems(world).ToList(),
        OpenDoors = ReadOpenDoors(world).ToList(),
    };

    internal static byte[] DecodeTileBytes(string base64, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        var expectedLength = checked(width * height);
        if (bytes.Length != expectedLength)
        {
            throw new InvalidDataException($"Tile data size mismatch: expected {expectedLength}, got {bytes.Length}.");
        }

        return bytes;
    }

    internal static byte[] DecodeFlagBytes(string base64, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        var expectedLength = checked(((width * height) + 7) / 8);
        if (bytes.Length != expectedLength)
        {
            throw new InvalidDataException($"Flag data size mismatch: expected {expectedLength}, got {bytes.Length}.");
        }

        return bytes;
    }

    private static EntitySaveData ToSaveData(IEntity entity, WorldState world)
    {
        var inventory = entity.GetComponent<InventoryComponent>();
        var statusEffects = entity.GetComponent<StatusEffectsComponent>();
        var progression = entity.GetComponent<ProgressionComponent>();
        var identity = entity.GetComponent<IdentityComponent>();
        var wallet = entity.GetComponent<WalletComponent>();
        var npc = entity.GetComponent<NpcComponent>();
        var merchant = entity.GetComponent<MerchantComponent>();
        var chest = entity.GetComponent<ChestComponent>();
        var xpValue = entity.GetComponent<XpValueComponent>();
        var abilities = entity.GetComponent<AbilitiesComponent>();
        var cooldowns = entity.GetComponent<CooldownComponent>();
        var aiState = entity.GetComponent<AIStateComponent>();
        var brain = entity.GetComponent<IBrain>();
        var enemy = entity.GetComponent<EnemyComponent>();
        var trap = entity.GetComponent<TrapComponent>();

        return new EntitySaveData
        {
            Id = entity.Id.Value.ToString("N"),
            Name = entity.Name,
            Position = new PositionSaveData { X = entity.Position.X, Y = entity.Position.Y },
            Faction = (int)entity.Faction,
            BlocksMovement = entity.BlocksMovement,
            BlocksSight = entity.BlocksSight,
            Stats = new StatsSaveData
            {
                HP = entity.Stats.HP,
                MaxHP = entity.Stats.MaxHP,
                Attack = entity.Stats.Attack,
                Accuracy = entity.Stats.Accuracy,
                Defense = entity.Stats.Defense,
                Evasion = entity.Stats.Evasion,
                Speed = entity.Stats.Speed,
                ViewRadius = entity.Stats.ViewRadius,
                Energy = entity.Stats.Energy,
            },
            Inventory = inventory is null ? null : ToSaveData(inventory),
            StatusEffects = statusEffects is null ? null : ToSaveData(statusEffects),
            Progression = progression is null ? null : new ProgressionSaveData
            {
                Level = progression.Level,
                Experience = progression.Experience,
                ExperienceToNextLevel = progression.ExperienceToNextLevel,
                UnspentStatPoints = progression.UnspentStatPoints,
                UnspentPerkChoices = progression.UnspentPerkChoices,
                Kills = progression.Kills,
                SelectedPerkIds = progression.SelectedPerkIds.ToList(),
            },
            Identity = identity is null ? null : new IdentitySaveData
            {
                RaceId = identity.RaceId,
                GenderId = identity.GenderId,
                AppearanceId = identity.AppearanceId,
                SpriteVariantId = identity.SpriteVariantId,
            },
            Wallet = wallet is null ? null : new WalletSaveData
            {
                Gold = wallet.Gold,
            },
            Npc = npc is null ? null : new NpcSaveData
            {
                TemplateId = npc.TemplateId,
                Role = npc.Role,
                DialogueId = npc.DialogueId,
            },
            Merchant = merchant is null ? null : new MerchantSaveData
            {
                Offers = merchant.Offers.Select(offer => new MerchantOfferSaveData
                {
                    ItemTemplateId = offer.ItemTemplateId,
                    Price = offer.Price,
                    Quantity = offer.Quantity,
                }).ToList(),
            },
            Chest = chest is null ? null : new ChestSaveData
            {
                LootTableId = chest.LootTableId,
            },
            XpValue = xpValue is null ? null : new XpValueSaveData
            {
                Value = xpValue.Value,
            },
            Abilities = abilities is null ? null : new AbilitiesSaveData
            {
                Slots = abilities.Slots.Select(slot => new AbilitySlotSaveData
                {
                    AbilityId = slot.AbilityId,
                    Cooldown = slot.Cooldown,
                    Priority = slot.Priority,
                }).ToList(),
            },
            Cooldowns = cooldowns is null ? null : new CooldownsSaveData
            {
                Active = cooldowns.ActiveCooldowns
                    .Where(pair => pair.Value > 0)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            },
            AIState = aiState is null ? null : new AIStateSaveData
            {
                State = (int)aiState.State,
                IdleTurns = aiState.IdleTurns,
                PatrolTarget = new PositionSaveData { X = aiState.PatrolTarget.X, Y = aiState.PatrolTarget.Y },
                PatrolSteps = aiState.PatrolSteps,
                PatrolSequence = aiState.PatrolSequence,
                LastKnownTargetPosition = new PositionSaveData { X = aiState.LastKnownTargetPosition.X, Y = aiState.LastKnownTargetPosition.Y },
                TargetId = aiState.TargetId.IsValid ? aiState.TargetId.Value.ToString("N") : string.Empty,
            },
            BrainType = brain is null ? string.Empty : ToBrainType(brain),
            Enemy = enemy is null ? null : new EnemySaveData
            {
                TemplateId = enemy.TemplateId,
            },
            Trap = trap is null ? null : new TrapSaveData
            {
                TemplateId = trap.TemplateId,
                IsArmed = trap.IsArmed,
                IsRevealed = trap.IsRevealed,
                TriggerCount = trap.TriggerCount,
            },
            SchedulerOrder = world.SchedulerOrders.TryGetValue(entity.Id, out var schedulerOrder) ? schedulerOrder : 0,
        };
    }

    private static InventorySaveData ToSaveData(InventoryComponent inventory)
    {
        return new InventorySaveData
        {
            Capacity = inventory.Capacity,
            Items = inventory.Items.Select(ToSaveData).ToList(),
            Equipped = inventory.EquippedItems
                .OrderBy(pair => (int)pair.Key)
                .Select(pair => new EquippedItemSaveData
                {
                    ItemId = pair.Value.Item.InstanceId.Value.ToString("N"),
                    Slot = (int)pair.Key,
                    StatModifiers = new Dictionary<string, int>(pair.Value.StatModifiers, StringComparer.OrdinalIgnoreCase),
                })
                .ToList(),
        };
    }

    private static StatusEffectsSaveData ToSaveData(StatusEffectsComponent statusEffects)
    {
        return new StatusEffectsSaveData
        {
            Effects = statusEffects.Effects.Select(effect => new StatusEffectSaveData
            {
                Type = (int)effect.Type,
                RemainingTurns = effect.RemainingTurns,
                Magnitude = effect.Magnitude,
                SourceEntityId = effect.SourceEntityId?.Value.ToString("N") ?? string.Empty,
            }).ToList(),
        };
    }

    private static ItemSaveData ToSaveData(ItemInstance item)
    {
        return new ItemSaveData
        {
            InstanceId = item.InstanceId.Value.ToString("N"),
            TemplateId = item.TemplateId,
            CurrentCharges = item.CurrentCharges,
            StackCount = item.StackCount,
            IsIdentified = item.IsIdentified,
        };
    }

    private static IEnumerable<GroundItemSaveData> FlattenGroundItems(WorldState world)
    {
        foreach (var entry in world.GetGroundItems().OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            foreach (var item in entry.Value)
            {
                yield return new GroundItemSaveData
                {
                    Position = new PositionSaveData { X = entry.Key.X, Y = entry.Key.Y },
                    Item = ToSaveData(item),
                };
            }
        }
    }

    private static IEnumerable<PositionSaveData> ReadOpenDoors(WorldState world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) == TileType.Door && world.IsDoorOpen(position))
                {
                    yield return new PositionSaveData { X = x, Y = y };
                }
            }
        }
    }

    private static bool[] ReadVisible(WorldState world)
    {
        var values = new bool[checked(world.Width * world.Height)];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var index = (y * world.Width) + x;
                values[index] = world.IsVisible(new Position(x, y));
            }
        }

        return values;
    }

    private static string EncodeTiles(IReadOnlyList<TileType> tiles)
    {
        var bytes = new byte[tiles.Count];
        for (var index = 0; index < tiles.Count; index++)
        {
            bytes[index] = (byte)tiles[index];
        }

        return Convert.ToBase64String(bytes);
    }

    private static string EncodeFlags(IReadOnlyList<bool> flags)
    {
        var bytes = new byte[(flags.Count + 7) / 8];
        for (var index = 0; index < flags.Count; index++)
        {
            if (flags[index])
            {
                bytes[index / 8] |= (byte)(1 << (index % 8));
            }
        }

        return Convert.ToBase64String(bytes);
    }

    private static TileType[] DecodeTiles(string base64, int width, int height)
    {
        var bytes = DecodeTileBytes(base64, width, height);
        var tiles = new TileType[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            tiles[index] = (TileType)bytes[index];
        }

        return tiles;
    }

    private static bool[] DecodeFlags(string base64, int width, int height)
    {
        var bytes = DecodeFlagBytes(base64, width, height);
        var totalCells = checked(width * height);
        var flags = new bool[totalCells];
        for (var index = 0; index < totalCells; index++)
        {
            flags[index] = (bytes[index / 8] & (1 << (index % 8))) != 0;
        }

        return flags;
    }

    private static IEntity ToEntity(EntitySaveData data) => ToEntity(data, null);

    private static IEntity ToEntity(EntitySaveData data, IContentDatabase? content)
    {
        var entity = new Entity(
            data.Name,
            new Position(data.Position.X, data.Position.Y),
            new Stats
            {
                HP = data.Stats.HP,
                MaxHP = data.Stats.MaxHP,
                Attack = data.Stats.Attack,
                Accuracy = data.Stats.Accuracy,
                Defense = data.Stats.Defense,
                Evasion = data.Stats.Evasion,
                Speed = data.Stats.Speed,
                ViewRadius = data.Stats.ViewRadius,
                Energy = data.Stats.Energy,
            },
            (Faction)data.Faction,
            data.BlocksMovement,
            data.BlocksSight,
            EntityId.From(data.Id));

        if (data.Inventory is not null)
        {
            var inventory = new InventoryComponent(data.Inventory.Capacity);
            foreach (var itemData in data.Inventory.Items)
            {
                inventory.Add(ToItemInstance(itemData));
            }

            foreach (var equipped in data.Inventory.Equipped)
            {
                var instanceId = EntityId.From(equipped.ItemId);
                var item = inventory.Get(instanceId) ?? throw new InvalidDataException($"Equipped item {equipped.ItemId} was not found in the inventory payload.");
                if (!inventory.TryEquip(item, (EquipSlot)equipped.Slot, equipped.StatModifiers, out _))
                {
                    throw new InvalidDataException($"Failed to restore equipped item {equipped.ItemId}.");
                }
            }

            entity.SetComponent(inventory);
        }

        if (data.StatusEffects is not null)
        {
            var statusEffects = new StatusEffectsComponent();
            foreach (var effect in data.StatusEffects.Effects)
            {
                EntityId? sourceId = string.IsNullOrWhiteSpace(effect.SourceEntityId) ? null : EntityId.From(effect.SourceEntityId);
                statusEffects.Add(new StatusEffectInstance((StatusEffectType)effect.Type, effect.RemainingTurns, effect.Magnitude, sourceId));
            }

            entity.SetComponent(statusEffects);
        }

        if (data.Progression is not null)
        {
            var progression = new ProgressionComponent
            {
                Level = data.Progression.Level,
                Experience = data.Progression.Experience,
                ExperienceToNextLevel = data.Progression.ExperienceToNextLevel,
                UnspentStatPoints = data.Progression.UnspentStatPoints,
                UnspentPerkChoices = data.Progression.UnspentPerkChoices,
                Kills = data.Progression.Kills,
            };

            foreach (var perkId in data.Progression.SelectedPerkIds)
            {
                progression.SelectedPerkIds.Add(perkId);
            }

            entity.SetComponent(progression);
        }

        if (data.Identity is not null)
        {
            entity.SetComponent(new IdentityComponent
            {
                RaceId = data.Identity.RaceId,
                GenderId = data.Identity.GenderId,
                AppearanceId = data.Identity.AppearanceId,
                SpriteVariantId = data.Identity.SpriteVariantId,
            });
        }

        if (data.Wallet is not null)
        {
            entity.SetComponent(new WalletComponent
            {
                Gold = data.Wallet.Gold,
            });
        }

        if (data.Npc is not null)
        {
            entity.SetComponent(new NpcComponent
            {
                TemplateId = data.Npc.TemplateId,
                Role = data.Npc.Role,
                DialogueId = data.Npc.DialogueId,
            });
        }

        if (data.Merchant is not null)
        {
            entity.SetComponent(new MerchantComponent(data.Merchant.Offers.Select(offer => new MerchantOfferState
            {
                ItemTemplateId = offer.ItemTemplateId,
                Price = offer.Price,
                Quantity = offer.Quantity,
            })));
        }

        if (data.Chest is not null)
        {
            entity.SetComponent(new ChestComponent { LootTableId = data.Chest.LootTableId });
        }

        if (data.XpValue is not null)
        {
            entity.SetComponent(new XpValueComponent { Value = data.XpValue.Value });
        }

        if (data.Abilities is not null)
        {
            var abilities = new AbilitiesComponent();
            foreach (var slot in data.Abilities.Slots)
            {
                abilities.Slots.Add(new EnemyAbilitySlot
                {
                    AbilityId = slot.AbilityId,
                    Cooldown = slot.Cooldown,
                    Priority = slot.Priority,
                });
            }

            entity.SetComponent(abilities);
        }

        if (data.Cooldowns is not null)
        {
            var cooldowns = new CooldownComponent();
            foreach (var pair in data.Cooldowns.Active)
            {
                cooldowns.SetCooldown(pair.Key, pair.Value);
            }

            entity.SetComponent(cooldowns);
        }

        if (data.AIState is not null)
        {
            entity.SetComponent(new AIStateComponent
            {
                State = (AIState)data.AIState.State,
                IdleTurns = data.AIState.IdleTurns,
                PatrolTarget = new Position(data.AIState.PatrolTarget.X, data.AIState.PatrolTarget.Y),
                PatrolSteps = data.AIState.PatrolSteps,
                PatrolSequence = data.AIState.PatrolSequence,
                LastKnownTargetPosition = new Position(data.AIState.LastKnownTargetPosition.X, data.AIState.LastKnownTargetPosition.Y),
                TargetId = string.IsNullOrWhiteSpace(data.AIState.TargetId) ? EntityId.Invalid : EntityId.From(data.AIState.TargetId),
            });
        }

        if (!string.IsNullOrWhiteSpace(data.BrainType))
        {
            var brain = TryCreateBrainFromTemplate(data, content) ?? BrainFactory.Create(data.BrainType);
            entity.SetComponent<IBrain>(brain);
        }

        if (data.Enemy is not null)
        {
            entity.SetComponent(new EnemyComponent { TemplateId = data.Enemy.TemplateId });
        }

        if (data.Trap is not null)
        {
            entity.SetComponent(new TrapComponent
            {
                TemplateId = data.Trap.TemplateId,
                IsArmed = data.Trap.IsArmed,
                IsRevealed = data.Trap.IsRevealed,
                TriggerCount = data.Trap.TriggerCount,
            });
        }

        return entity;
    }

    private static string ToBrainType(IBrain brain) => brain switch
    {
        RangedKiterBrain => "ranged_kiter",
        PatrolGuardBrain => "patrol_guard",
        FleeingBrain => "fleeing",
        AmbushBrain => "ambush",
        SupportBrain => "support",
        MeleeRusherBrain => "melee_rusher",
        _ => string.Empty,
    };

    private static IBrain? TryCreateBrainFromTemplate(EntitySaveData data, IContentDatabase? content)
    {
        if (content is null || data.Enemy is null || string.IsNullOrWhiteSpace(data.Enemy.TemplateId))
        {
            return null;
        }

        if (!content.TryGetEnemyTemplate(data.Enemy.TemplateId, out var template))
        {
            return null;
        }

        return BrainFactory.Create(template);
    }

    private static ItemInstance ToItemInstance(ItemSaveData data)
    {
        return new ItemInstance
        {
            InstanceId = EntityId.From(data.InstanceId),
            TemplateId = data.TemplateId,
            CurrentCharges = data.CurrentCharges,
            StackCount = data.StackCount,
            IsIdentified = data.IsIdentified,
        };
    }

    private static Position IndexToPosition(int index, int width) => new(index % width, index / width);
}
