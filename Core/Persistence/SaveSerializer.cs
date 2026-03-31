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

    public int Seed { get; set; }

    public int Depth { get; set; }

    public int TurnNumber { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string Tiles { get; set; } = string.Empty;

    public string Explored { get; set; } = string.Empty;

    public string Visible { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

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
}

internal sealed class ProgressionSaveData
{
    public int Level { get; set; }

    public int Experience { get; set; }

    public int ExperienceToNextLevel { get; set; }

    public int UnspentStatPoints { get; set; }

    public int Kills { get; set; }
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
    public const int CurrentVersion = 4;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    internal static SaveFileData CreateSaveData(WorldState world, DateTime savedAt)
    {
        var data = new SaveFileData
        {
            Version = CurrentVersion,
            SavedAt = savedAt,
            Seed = world.Seed,
            Depth = world.Depth,
            TurnNumber = world.TurnNumber,
            Width = world.Width,
            Height = world.Height,
            Tiles = EncodeTiles(world.GetRawGrid()),
            Explored = EncodeFlags(world.GetRawExplored()),
            Visible = EncodeFlags(ReadVisible(world)),
            PlayerId = world.Player.Id.Value.ToString("N"),
            Entities = world.Entities.Select(ToSaveData).ToList(),
            GroundItems = FlattenGroundItems(world).ToList(),
            OpenDoors = ReadOpenDoors(world).ToList(),
        };

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
        return new SaveMetadata(slotIndex, data.Depth, data.TurnNumber, playerName, data.SavedAt, data.Version);
    }

    internal static WorldState ToWorldState(SaveFileData data)
    {
        var world = new WorldState();
        world.InitGrid(data.Width, data.Height);
        world.Seed = data.Seed;
        world.Depth = data.Depth;
        world.TurnNumber = data.TurnNumber;

        var tiles = DecodeTiles(data.Tiles, data.Width, data.Height);
        for (var index = 0; index < tiles.Length; index++)
        {
            world.SetTile(IndexToPosition(index, data.Width), tiles[index]);
        }

        var explored = DecodeFlags(data.Explored, data.Width, data.Height);
        for (var index = 0; index < explored.Length; index++)
        {
            if (explored[index])
            {
                world.SetVisible(IndexToPosition(index, data.Width), true);
            }
        }

        world.ClearVisibility();

        var visible = DecodeFlags(data.Visible, data.Width, data.Height);
        for (var index = 0; index < visible.Length; index++)
        {
            if (visible[index])
            {
                world.SetVisible(IndexToPosition(index, data.Width), true);
            }
        }

        foreach (var door in data.OpenDoors)
        {
            world.SetDoorOpen(new Position(door.X, door.Y), true);
        }

        foreach (var entityData in data.Entities)
        {
            world.AddEntity(ToEntity(entityData));
        }

        var playerId = EntityId.From(data.PlayerId);
        world.Player = world.GetEntity(playerId) ?? throw new InvalidDataException("Player entity missing from save data.");

        foreach (var groundItem in data.GroundItems)
        {
            world.DropItem(new Position(groundItem.Position.X, groundItem.Position.Y), ToItemInstance(groundItem.Item));
        }

        return world;
    }

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

    private static EntitySaveData ToSaveData(IEntity entity)
    {
        var inventory = entity.GetComponent<InventoryComponent>();
        var statusEffects = entity.GetComponent<StatusEffectsComponent>();
        var progression = entity.GetComponent<ProgressionComponent>();
        var identity = entity.GetComponent<IdentityComponent>();

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
                Kills = progression.Kills,
            },
            Identity = identity is null ? null : new IdentitySaveData
            {
                RaceId = identity.RaceId,
                GenderId = identity.GenderId,
                AppearanceId = identity.AppearanceId,
                SpriteVariantId = identity.SpriteVariantId,
            },
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

    private static IEntity ToEntity(EntitySaveData data)
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
                statusEffects.Add(new StatusEffectInstance((StatusEffectType)effect.Type, effect.RemainingTurns, effect.Magnitude));
            }

            entity.SetComponent(statusEffects);
        }

        if (data.Progression is not null)
        {
            entity.SetComponent(new ProgressionComponent
            {
                Level = data.Progression.Level,
                Experience = data.Progression.Experience,
                ExperienceToNextLevel = data.Progression.ExperienceToNextLevel,
                UnspentStatPoints = data.Progression.UnspentStatPoints,
                Kills = data.Progression.Kills,
            });
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

        return entity;
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