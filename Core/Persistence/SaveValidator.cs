using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Roguelike.Core;

public static class SaveValidator
{
    internal static IReadOnlyList<string> Validate(SaveFileData data)
    {
        var errors = new List<string>();

        if (data.Version != SaveSerializer.CurrentVersion)
        {
            errors.Add($"Unsupported normalized save version {data.Version}.");
        }

        if (data.SavedAt == default)
        {
            errors.Add("SavedAt timestamp is required.");
        }

        if (string.IsNullOrWhiteSpace(data.PlayerId))
        {
            errors.Add("PlayerId is required.");
        }

        if (data.Floors.Count > 0)
        {
            ValidateFloors(data, errors);
            return errors;
        }

        if (data.Width <= 0 || data.Height <= 0)
        {
            errors.Add("Map dimensions must be positive.");
            return errors;
        }

        try
        {
            ValidateTiles(data, errors);
            ValidateFlags(data.Explored, data.Width, data.Height, "explored", errors);
            ValidateFlags(data.Visible, data.Width, data.Height, "visible", errors);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or OverflowException)
        {
            errors.Add(ex.Message);
        }

        ValidateEntities(data, errors, requirePlayer: true);
        ValidateGroundItems(data, errors);
        ValidateOpenDoors(data, errors);

        return errors;
    }

    private static void ValidateTiles(SaveFileData data, List<string> errors)
    {
        var tileBytes = SaveSerializer.DecodeTileBytes(data.Tiles, data.Width, data.Height);
        for (var index = 0; index < tileBytes.Length; index++)
        {
            if (!IsDefinedEnumValue<TileType>(tileBytes[index]))
            {
                errors.Add($"Tile payload contains invalid tile value {tileBytes[index]} at index {index}.");
            }
        }
    }

    private static void ValidateFlags(string payload, int width, int height, string label, List<string> errors)
    {
        SaveSerializer.DecodeFlagBytes(payload, width, height);
    }

    private static void ValidateFloors(SaveFileData data, List<string> errors)
    {
        var depths = new HashSet<int>();
        var activeFloorFound = false;
        var activeFloorHasPlayer = false;
        var playerCount = 0;

        foreach (var floor in data.Floors)
        {
            if (!depths.Add(floor.Depth))
            {
                errors.Add($"Duplicate floor depth {floor.Depth}.");
            }

            var floorData = new SaveFileData
            {
                Version = data.Version,
                SavedAt = data.SavedAt,
                Seed = data.Seed,
                Depth = floor.Depth,
                TurnNumber = floor.TurnNumber,
                CombatRandomState = floor.CombatRandomState,
                ItemRandomState = floor.ItemRandomState,
                Width = floor.Width,
                Height = floor.Height,
                Tiles = floor.Tiles,
                Explored = floor.Explored,
                Visible = floor.Visible,
                PlayerId = data.PlayerId,
                Entities = floor.Entities,
                GroundItems = floor.GroundItems,
                OpenDoors = floor.OpenDoors,
            };

            if (floorData.Width <= 0 || floorData.Height <= 0)
            {
                errors.Add($"Floor {floor.Depth} map dimensions must be positive.");
                continue;
            }

            if (floor.SchedulerNextOrder < 0)
            {
                errors.Add($"Floor {floor.Depth} has a negative scheduler next order {floor.SchedulerNextOrder}.");
            }

            try
            {
                ValidateTiles(floorData, errors);
                ValidateFlags(floorData.Explored, floorData.Width, floorData.Height, "explored", errors);
                ValidateFlags(floorData.Visible, floorData.Width, floorData.Height, "visible", errors);
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or OverflowException)
            {
                errors.Add(ex.Message);
            }

            var floorHasPlayer = floorData.Entities.Any(entity => string.Equals(entity.Id, data.PlayerId, StringComparison.OrdinalIgnoreCase));
            if (floorHasPlayer)
            {
                playerCount++;
            }

            if (floor.Depth == data.Depth)
            {
                activeFloorFound = true;
                activeFloorHasPlayer = floorHasPlayer;
            }

            ValidateEntities(floorData, errors, requirePlayer: false);
            ValidateGroundItems(floorData, errors);
            ValidateOpenDoors(floorData, errors);
        }

        if (!activeFloorFound)
        {
            errors.Add($"Active floor {data.Depth} is missing from floor payloads.");
        }

        if (!activeFloorHasPlayer)
        {
            errors.Add("PlayerId does not resolve to an entity on the active floor.");
        }

        if (playerCount != 1)
        {
            errors.Add($"PlayerId must appear exactly once across saved floors, found {playerCount}.");
        }
    }

    private static void ValidateEntities(SaveFileData data, List<string> errors, bool requirePlayer)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blockingPositions = new HashSet<Position>();
        var playerFound = false;

        foreach (var entity in data.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Id) || !Guid.TryParse(entity.Id, out _))
            {
                errors.Add($"Entity '{entity.Name}' is missing a valid id.");
                continue;
            }

            if (!ids.Add(entity.Id))
            {
                errors.Add($"Duplicate entity id '{entity.Id}'.");
            }

            if (string.Equals(entity.Id, data.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                playerFound = true;
            }

            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                errors.Add($"Entity '{entity.Id}' is missing a name.");
            }

            if (!IsDefinedEnumValue<Faction>(entity.Faction))
            {
                errors.Add($"Entity '{entity.Name}' has an invalid faction value {entity.Faction}.");
            }

            if (!IsInBounds(entity.Position, data.Width, data.Height))
            {
                errors.Add($"Entity '{entity.Name}' is out of bounds at ({entity.Position.X},{entity.Position.Y}).");
            }
            else if (entity.BlocksMovement)
            {
                var position = new Position(entity.Position.X, entity.Position.Y);
                if (!blockingPositions.Add(position))
                {
                    errors.Add($"Multiple blocking entities occupy ({entity.Position.X},{entity.Position.Y}).");
                }
            }

            ValidateStats(entity, errors);
            ValidateInventory(entity, errors);
            ValidateStatusEffects(entity, errors);
            ValidateNewComponentPayloads(data, entity, errors);
        }

        if (requirePlayer && !playerFound)
        {
            errors.Add("PlayerId does not resolve to an entity in the save.");
        }
    }

    private static void ValidateStats(EntitySaveData entity, List<string> errors)
    {
        if (entity.Stats.MaxHP <= 0)
        {
            errors.Add($"Entity '{entity.Name}' must have positive MaxHP.");
        }

        if (entity.Stats.HP < 0 || entity.Stats.HP > entity.Stats.MaxHP)
        {
            errors.Add($"Entity '{entity.Name}' has an invalid HP value {entity.Stats.HP}.");
        }

        if (entity.Stats.Speed < 0 || (entity.Stats.Speed == 0 && entity.Chest is null && entity.Trap is null))
        {
            errors.Add($"Entity '{entity.Name}' must have positive speed unless it is a chest or trap.");
        }

        if (entity.Stats.ViewRadius < 0)
        {
            errors.Add($"Entity '{entity.Name}' has an invalid view radius {entity.Stats.ViewRadius}.");
        }
    }

    private static void ValidateInventory(EntitySaveData entity, List<string> errors)
    {
        if (entity.Inventory is null)
        {
            return;
        }

        if (entity.Inventory.Capacity < 0)
        {
            errors.Add($"Entity '{entity.Name}' has a negative inventory capacity.");
        }

        if (entity.Inventory.Items.Count > entity.Inventory.Capacity)
        {
            errors.Add($"Entity '{entity.Name}' exceeds inventory capacity.");
        }

        var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in entity.Inventory.Items)
        {
            if (string.IsNullOrWhiteSpace(item.InstanceId) || !Guid.TryParse(item.InstanceId, out _))
            {
                errors.Add($"Entity '{entity.Name}' contains an inventory item with an invalid id.");
                continue;
            }

            if (!itemIds.Add(item.InstanceId))
            {
                errors.Add($"Entity '{entity.Name}' contains duplicate inventory item id '{item.InstanceId}'.");
            }

            if (string.IsNullOrWhiteSpace(item.TemplateId))
            {
                errors.Add($"Entity '{entity.Name}' contains an inventory item without a template id.");
            }

            if (item.StackCount <= 0)
            {
                errors.Add($"Entity '{entity.Name}' contains an inventory item with invalid stack count {item.StackCount}.");
            }
        }

        var usedSlots = new HashSet<int>();
        foreach (var equipped in entity.Inventory.Equipped)
        {
            if (!itemIds.Contains(equipped.ItemId))
            {
                errors.Add($"Entity '{entity.Name}' equips missing inventory item '{equipped.ItemId}'.");
            }

            if (!IsDefinedEnumValue<EquipSlot>(equipped.Slot) || equipped.Slot == (int)EquipSlot.None)
            {
                errors.Add($"Entity '{entity.Name}' has an invalid equipped slot {equipped.Slot}.");
            }

            if (!usedSlots.Add(equipped.Slot))
            {
                errors.Add($"Entity '{entity.Name}' equips multiple items into slot {equipped.Slot}.");
            }
        }
    }

    private static void ValidateStatusEffects(EntitySaveData entity, List<string> errors)
    {
        if (entity.StatusEffects is null)
        {
            return;
        }

        foreach (var effect in entity.StatusEffects.Effects)
        {
            if (!IsDefinedEnumValue<StatusEffectType>(effect.Type) || effect.Type == (int)StatusEffectType.None)
            {
                errors.Add($"Entity '{entity.Name}' has an invalid status effect type {effect.Type}.");
            }

            if (effect.RemainingTurns <= 0)
            {
                errors.Add($"Entity '{entity.Name}' has a status effect with invalid duration {effect.RemainingTurns}.");
            }

            if (effect.Magnitude <= 0)
            {
                errors.Add($"Entity '{entity.Name}' has a status effect with invalid magnitude {effect.Magnitude}.");
            }
        }
    }

    private static void ValidateNewComponentPayloads(SaveFileData data, EntitySaveData entity, List<string> errors)
    {
        if (entity.Chest is not null)
        {
            if (string.IsNullOrWhiteSpace(entity.Chest.LootTableId))
            {
                errors.Add($"Entity '{entity.Name}' has a chest without a loot table id.");
            }

            ValidateChest(entity, errors);
        }

        if (entity.XpValue is not null && entity.XpValue.Value < 0)
        {
            errors.Add($"Entity '{entity.Name}' has a negative XP value {entity.XpValue.Value}.");
        }

        if (entity.Abilities is not null)
        {
            foreach (var slot in entity.Abilities.Slots)
            {
                if (string.IsNullOrWhiteSpace(slot.AbilityId))
                {
                    errors.Add($"Entity '{entity.Name}' has an ability slot without an ability id.");
                }

                if (slot.Cooldown < 0)
                {
                    errors.Add($"Entity '{entity.Name}' has ability '{slot.AbilityId}' with negative cooldown {slot.Cooldown}.");
                }

                if (slot.Priority < 0)
                {
                    errors.Add($"Entity '{entity.Name}' has ability '{slot.AbilityId}' with negative priority {slot.Priority}.");
                }
            }
        }

        if (entity.Cooldowns is not null)
        {
            foreach (var cooldown in entity.Cooldowns.Active)
            {
                if (string.IsNullOrWhiteSpace(cooldown.Key))
                {
                    errors.Add($"Entity '{entity.Name}' has an active cooldown without an ability id.");
                }

                if (cooldown.Value < 0)
                {
                    errors.Add($"Entity '{entity.Name}' has active cooldown '{cooldown.Key}' with negative value {cooldown.Value}.");
                }
            }
        }

        if (entity.AIState is not null)
        {
            if (!IsDefinedEnumValue<AIState>(entity.AIState.State))
            {
                errors.Add($"Entity '{entity.Name}' has an invalid AI state value {entity.AIState.State}.");
            }

            if (!string.IsNullOrWhiteSpace(entity.AIState.TargetId) && !Guid.TryParse(entity.AIState.TargetId, out _))
            {
                errors.Add($"Entity '{entity.Name}' has an invalid AI target id '{entity.AIState.TargetId}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(entity.BrainType) && !IsAllowedBrainType(entity.BrainType))
        {
            errors.Add($"Entity '{entity.Name}' has an invalid brain type '{entity.BrainType}'.");
        }

        if (entity.Enemy is not null && string.IsNullOrWhiteSpace(entity.Enemy.TemplateId))
        {
            errors.Add($"Entity '{entity.Name}' has an enemy component without a template id.");
        }

        if (entity.Trap is not null)
        {
            if (string.IsNullOrWhiteSpace(entity.Trap.TemplateId))
            {
                errors.Add($"Entity '{entity.Name}' has a trap component without a template id.");
            }

            if (!IsInBounds(entity.Position, data.Width, data.Height))
            {
                errors.Add($"Entity '{entity.Name}' trap position is out of bounds.");
            }
            else
            {
                var tileBytes = SaveSerializer.DecodeTileBytes(data.Tiles, data.Width, data.Height);
                var tileIndex = (entity.Position.Y * data.Width) + entity.Position.X;
                if ((TileType)tileBytes[tileIndex] != TileType.Trap)
                {
                    errors.Add($"Entity '{entity.Name}' has a trap component on a non-trap tile.");
                }
            }
        }

        if (entity.SchedulerOrder < 0)
        {
            errors.Add($"Entity '{entity.Name}' has a negative scheduler order {entity.SchedulerOrder}.");
        }
    }

    private static void ValidateChest(EntitySaveData entity, List<string> errors)
    {
        var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in entity.Chest!.Contents)
        {
            if (string.IsNullOrWhiteSpace(item.InstanceId) || !Guid.TryParse(item.InstanceId, out _))
            {
                errors.Add($"Entity '{entity.Name}' contains a chest item with an invalid id.");
                continue;
            }

            if (!itemIds.Add(item.InstanceId))
            {
                errors.Add($"Entity '{entity.Name}' contains duplicate chest item id '{item.InstanceId}'.");
            }

            if (string.IsNullOrWhiteSpace(item.TemplateId))
            {
                errors.Add($"Entity '{entity.Name}' contains a chest item without a template id.");
            }

            if (item.StackCount <= 0)
            {
                errors.Add($"Entity '{entity.Name}' contains a chest item with invalid stack count {item.StackCount}.");
            }
        }
    }

    private static void ValidateGroundItems(SaveFileData data, List<string> errors)
    {
        var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.GroundItems)
        {
            if (!IsInBounds(item.Position, data.Width, data.Height))
            {
                errors.Add($"Ground item is out of bounds at ({item.Position.X},{item.Position.Y}).");
            }

            if (string.IsNullOrWhiteSpace(item.Item.InstanceId) || !Guid.TryParse(item.Item.InstanceId, out _))
            {
                errors.Add("Ground item is missing a valid instance id.");
            }
            else if (!itemIds.Add(item.Item.InstanceId))
            {
                errors.Add($"Duplicate ground item id '{item.Item.InstanceId}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Item.TemplateId))
            {
                errors.Add("Ground item is missing a template id.");
            }

            if (item.Item.StackCount <= 0)
            {
                errors.Add($"Ground item '{item.Item.InstanceId}' has invalid stack count {item.Item.StackCount}.");
            }
        }
    }

    private static void ValidateOpenDoors(SaveFileData data, List<string> errors)
    {
        var seen = new HashSet<Position>();
        var tileBytes = SaveSerializer.DecodeTileBytes(data.Tiles, data.Width, data.Height);
        foreach (var door in data.OpenDoors)
        {
            if (!IsInBounds(door, data.Width, data.Height))
            {
                errors.Add($"Open door is out of bounds at ({door.X},{door.Y}).");
                continue;
            }

            var position = new Position(door.X, door.Y);
            if (!seen.Add(position))
            {
                errors.Add($"Open door at ({door.X},{door.Y}) is duplicated.");
            }

            var tileIndex = (door.Y * data.Width) + door.X;
            if ((TileType)tileBytes[tileIndex] != TileType.Door)
            {
                errors.Add($"Open door at ({door.X},{door.Y}) does not correspond to a door tile.");
            }
        }
    }

    private static bool IsInBounds(PositionSaveData position, int width, int height) =>
        position.X >= 0 && position.X < width && position.Y >= 0 && position.Y < height;

    private static bool IsAllowedBrainType(string brainType) => brainType is
        "melee_rusher" or
        "ranged_kiter" or
        "patrol_guard" or
        "fleeing" or
        "ambush" or
        "support";

    private static bool IsDefinedEnumValue<TEnum>(int value)
        where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (Convert.ToInt32(candidate) == value)
            {
                return true;
            }
        }

        return false;
    }
}
