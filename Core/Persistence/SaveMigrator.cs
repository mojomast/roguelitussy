using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Roguelike.Core;

public static class SaveMigrator
{
    internal static SaveFileData MigrateToCurrent(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var version = ReadVersion(root);

        return version switch
        {
            1 => MigrateV1(root),
            2 => MigrateV2(root),
            3 => MigrateV3(root),
            4 => MigrateV4(root),
            5 => MigrateV5(root),
            6 => MigrateV6(root),
            7 => MigrateV7(root),
            8 => MigrateV8(root),
            9 => MigrateV9(root),
            10 => MigrateV10(root),
            11 => MigrateV11(root),
            12 => MigrateV12(root),
            SaveSerializer.CurrentVersion => JsonSerializer.Deserialize<SaveFileData>(json, SaveSerializer.JsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize save data."),
            13 => MigrateV13(root),
            14 => MigrateV14(root),
            _ => throw new InvalidOperationException($"Unsupported save version {version}.")
        };
    }

    private static SaveFileData MigrateV14(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 14 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static int ReadVersion(JsonElement root)
    {
        if (root.TryGetProperty("version", out var versionProperty) && versionProperty.ValueKind == JsonValueKind.Number && versionProperty.TryGetInt32(out var version))
        {
            return version;
        }

        return 1;
    }

    private static SaveFileData MigrateV1(JsonElement root)
    {
        var width = GetRequiredInt(root, "mapWidth");
        var height = GetRequiredInt(root, "mapHeight");
        var schedulerEnergy = ReadSchedulerEnergy(root);

        var data = new SaveFileData
        {
            Version = SaveSerializer.CurrentVersion,
            SavedAt = ReadDateTime(root, "timestamp"),
            Seed = GetRequiredInt(root, "seed"),
            Depth = GetRequiredInt(root, "currentFloor"),
            TurnNumber = GetRequiredInt(root, "turnNumber"),
            Width = width,
            Height = height,
            Tiles = GetRequiredString(root, "tiles"),
            Explored = ReencodeFlagsV2ToV3(GetRequiredString(root, "explored"), width, height),
            Visible = Convert.ToBase64String(new byte[checked(((width * height) + 7) / 8)]),
        };

        var entities = new List<EntitySaveData>();
        var player = MigrateLegacyEntity(root.GetProperty("player"), schedulerEnergy, true, 0, string.Empty);
        data.PlayerId = player.Id;
        entities.Add(player);

        var legacyEntities = root.TryGetProperty("entities", out var legacyEntityArray) && legacyEntityArray.ValueKind == JsonValueKind.Array
            ? legacyEntityArray
            : default;

        if (legacyEntities.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var element in legacyEntities.EnumerateArray())
            {
                entities.Add(MigrateLegacyEntity(element, schedulerEnergy, false, index++, string.Empty));
            }
        }

        data.Entities = entities;
        data.GroundItems = ReadLegacyGroundItems(root);
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV2(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 2 save data.");

        data.Explored = ReencodeFlagsV2ToV3(data.Explored, data.Width, data.Height);
        data.Visible = ReencodeFlagsV2ToV3(data.Visible, data.Width, data.Height);
        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV3(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 3 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV5(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 5 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV4(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 4 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV6(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 6 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        data.CombatRandomState = 0UL;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV7(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 7 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV8(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 8 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV9(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 9 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        data.CharacterOptions ??= new CharacterOptionsSaveData();
        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV10(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 10 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        var result = FinalizeSingleFloor(data);
        ConvertLegacyTrapsToEntities(result, root);
        return result;
    }

    private static SaveFileData MigrateV11(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 11 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        var result = FinalizeSingleFloor(data);
        ConvertLegacyTrapsToEntities(result, root);
        return result;
    }

    private static SaveFileData MigrateV12(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 12 save data.");

        return FinalizeSingleFloor(data);
    }

    private static SaveFileData MigrateV13(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 13 save data.");

        data.Version = SaveSerializer.CurrentVersion;
        return FinalizeSingleFloor(data);
    }

    private static void ConvertLegacyTrapsToEntities(SaveFileData data, JsonElement root)
    {
        var floorTrapPositions = new Dictionary<int, HashSet<Position>>();

        if (root.TryGetProperty("floors", out var floors) && floors.ValueKind == JsonValueKind.Array)
        {
            for (var index = 0; index < floors.GetArrayLength() && index < data.Floors.Count; index++)
            {
                var floor = floors[index];
                if (!floor.TryGetProperty("traps", out var traps) || traps.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var floorData = data.Floors[index];
                var seen = floorTrapPositions[index] = new HashSet<Position>(floorData.Entities.Select(entity => new Position(entity.Position.X, entity.Position.Y)));
                foreach (var trap in traps.EnumerateArray())
                {
                    var entity = LegacyTrapToEntity(trap);
                    var position = new Position(entity.Position.X, entity.Position.Y);
                    if (seen.Add(position))
                    {
                        floorData.Entities.Add(entity);
                    }
                }
            }
        }

        if (root.TryGetProperty("traps", out var rootTraps) && rootTraps.ValueKind == JsonValueKind.Array)
        {
            var activeFloorIndex = data.Floors.FindIndex(floor => floor.Depth == data.Depth);
            if (activeFloorIndex < 0)
            {
                activeFloorIndex = 0;
            }

            if (activeFloorIndex < data.Floors.Count)
            {
                var activeFloor = data.Floors[activeFloorIndex];
                if (!floorTrapPositions.TryGetValue(activeFloorIndex, out var seen))
                {
                    seen = new HashSet<Position>(activeFloor.Entities.Select(entity => new Position(entity.Position.X, entity.Position.Y)));
                    floorTrapPositions[activeFloorIndex] = seen;
                }

                foreach (var trap in rootTraps.EnumerateArray())
                {
                    var entity = LegacyTrapToEntity(trap);
                    var position = new Position(entity.Position.X, entity.Position.Y);
                    if (seen.Add(position))
                    {
                        activeFloor.Entities.Add(entity);
                    }
                }
            }
        }
    }

    private static EntitySaveData LegacyTrapToEntity(JsonElement trap)
    {
        var (x, y) = ReadLegacyTrapPosition(trap);
        var trapId = TryGetString(trap, "trapId") ?? string.Empty;
        var disarmed = TryGetBoolean(trap, "disarmed") ?? false;
        var triggered = TryGetBoolean(trap, "triggered") ?? false;

        return new EntitySaveData
        {
            Id = CreateStableEntityId($"trap:{x}:{y}:{trapId}"),
            Name = string.IsNullOrWhiteSpace(trapId) ? "Trap" : trapId,
            Position = new PositionSaveData { X = x, Y = y },
            Faction = (int)Faction.Neutral,
            BlocksMovement = false,
            BlocksSight = false,
            Stats = new StatsSaveData
            {
                HP = 1,
                MaxHP = 1,
                Attack = 0,
                Accuracy = 0,
                Defense = 0,
                Evasion = 0,
                Speed = 0,
                ViewRadius = 0,
                Energy = 0,
            },
            Trap = new TrapSaveData
            {
                TemplateId = trapId,
                IsArmed = !triggered && !disarmed,
                IsRevealed = triggered,
                TriggerCount = triggered ? 1 : 0,
            },
        };
    }

    private static (int X, int Y) ReadLegacyTrapPosition(JsonElement trap)
    {
        if (trap.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.Object)
        {
            var x = TryGetInt(position, "x") ?? 0;
            var y = TryGetInt(position, "y") ?? 0;
            return (x, y);
        }

        var fallbackX = TryGetInt(trap, "x") ?? 0;
        var fallbackY = TryGetInt(trap, "y") ?? 0;
        return (fallbackX, fallbackY);
    }

    private static SaveFileData FinalizeSingleFloor(SaveFileData data)
    {
        data.Version = SaveSerializer.CurrentVersion;
        if (data.Floors.Count == 0)
        {
            data.Floors.Add(SaveSerializer.CreateFloorFromRoot(data));
        }

        var activeFloor = data.Floors.Find(floor => floor.Depth == data.Depth) ?? data.Floors[0];
        SaveSerializer.ApplyActiveFloorAliases(data, activeFloor);
        return data;
    }

    private static EntitySaveData MigrateLegacyEntity(JsonElement element, IReadOnlyDictionary<string, int> schedulerEnergy, bool forceInventory, int entityIndex, string itemSeedPrefix)
    {
        var legacyId = ReadLegacyId(element.GetProperty("id"));
        var stats = new StatsSaveData
        {
            HP = GetRequiredInt(element, "hp"),
            MaxHP = GetRequiredInt(element, "maxHP"),
            Attack = GetRequiredInt(element, "attack"),
            Accuracy = TryGetInt(element, "accuracy") ?? 0,
            Defense = GetRequiredInt(element, "defense"),
            Evasion = TryGetInt(element, "evasion") ?? 0,
            Speed = GetRequiredInt(element, "speed"),
            ViewRadius = GetRequiredInt(element, "viewRadius"),
            Energy = TryGetInt(element, "energy") ?? ResolveLegacyEnergy(schedulerEnergy, legacyId),
        };

        var inventory = ReadLegacyInventory(element, legacyId, forceInventory || (TryGetInt(element, "maxInventorySize") ?? 0) > 0);
        return new EntitySaveData
        {
            Id = legacyId,
            Name = GetRequiredString(element, "name"),
            Position = new PositionSaveData
            {
                X = GetRequiredInt(element, "posX"),
                Y = GetRequiredInt(element, "posY"),
            },
            Faction = GetRequiredInt(element, "faction"),
            BlocksMovement = TryGetBoolean(element, "blocksMovement") ?? true,
            BlocksSight = TryGetBoolean(element, "blocksSight") ?? false,
            Stats = stats,
            Inventory = inventory,
            StatusEffects = ReadLegacyStatusEffects(element),
        };
    }

    private static InventorySaveData? ReadLegacyInventory(JsonElement element, string ownerId, bool allowEmpty)
    {
        var hasInventoryProperty = element.TryGetProperty("inventory", out var inventoryArray) && inventoryArray.ValueKind == JsonValueKind.Array;
        var capacity = TryGetInt(element, "maxInventorySize") ?? 0;

        if (!hasInventoryProperty && !allowEmpty)
        {
            return null;
        }

        var inventory = new InventorySaveData { Capacity = Math.Max(capacity, hasInventoryProperty ? inventoryArray.GetArrayLength() : 0) };

        if (hasInventoryProperty)
        {
            var index = 0;
            foreach (var item in inventoryArray.EnumerateArray())
            {
                var templateId = item.GetString() ?? string.Empty;
                inventory.Items.Add(new ItemSaveData
                {
                    InstanceId = CreateStableEntityId($"{ownerId}:inventory:{index}:{templateId}"),
                    TemplateId = templateId,
                    CurrentCharges = 0,
                    StackCount = 1,
                    IsIdentified = false,
                });

                index++;
            }
        }

        AttachLegacyEquipment(element, inventory, "equippedWeapon", EquipSlot.MainHand);
        AttachLegacyEquipment(element, inventory, "equippedArmor", EquipSlot.Body);
        return inventory;
    }

    private static void AttachLegacyEquipment(JsonElement element, InventorySaveData inventory, string propertyName, EquipSlot slot)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var templateId = property.GetString();
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        var item = inventory.Items.Find(candidate => string.Equals(candidate.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            item = new ItemSaveData
            {
                InstanceId = CreateStableEntityId($"{propertyName}:{templateId}"),
                TemplateId = templateId,
                CurrentCharges = 0,
                StackCount = 1,
                IsIdentified = false,
            };
            inventory.Items.Add(item);
        }

        inventory.Equipped.Add(new EquippedItemSaveData
        {
            ItemId = item.InstanceId,
            Slot = (int)slot,
        });
    }

    private static StatusEffectsSaveData? ReadLegacyStatusEffects(JsonElement element)
    {
        if (!element.TryGetProperty("statusEffects", out var statusEffects) || statusEffects.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var data = new StatusEffectsSaveData();
        foreach (var effect in statusEffects.EnumerateArray())
        {
            data.Effects.Add(new StatusEffectSaveData
            {
                Type = GetRequiredInt(effect, "type"),
                RemainingTurns = GetRequiredInt(effect, "remainingTurns"),
                Magnitude = TryGetInt(effect, "magnitude") ?? TryGetInt(effect, "stacks") ?? 1,
            });
        }

        return data;
    }

    private static List<GroundItemSaveData> ReadLegacyGroundItems(JsonElement root)
    {
        var items = new List<GroundItemSaveData>();
        if (!root.TryGetProperty("groundItems", out var groundItems) || groundItems.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        var index = 0;
        foreach (var item in groundItems.EnumerateArray())
        {
            var x = GetRequiredInt(item, "x");
            var y = GetRequiredInt(item, "y");
            var templateId = GetRequiredString(item, "itemId");
            items.Add(new GroundItemSaveData
            {
                Position = new PositionSaveData { X = x, Y = y },
                Item = new ItemSaveData
                {
                    InstanceId = CreateStableEntityId($"ground:{x}:{y}:{index}:{templateId}"),
                    TemplateId = templateId,
                    CurrentCharges = 0,
                    StackCount = 1,
                    IsIdentified = false,
                },
            });

            index++;
        }

        return items;
    }

    private static IReadOnlyDictionary<string, int> ReadSchedulerEnergy(JsonElement root)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("schedulerEnergy", out var energyRoot) || energyRoot.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in energyRoot.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var value))
            {
                result[ReadLegacyId(property.Name)] = value;
            }
        }

        return result;
    }

    private static int ResolveLegacyEnergy(IReadOnlyDictionary<string, int> schedulerEnergy, string entityId) =>
        schedulerEnergy.TryGetValue(entityId, out var value) ? value : 0;

    private static string ReencodeFlagsV2ToV3(string base64, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        var expectedLength = checked(width * height);
        if (bytes.Length != expectedLength)
        {
            throw new InvalidOperationException($"Version 2 flag data size mismatch: expected {expectedLength}, got {bytes.Length}.");
        }

        var packed = new byte[(bytes.Length + 7) / 8];
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == 1)
            {
                packed[index / 8] |= (byte)(1 << (index % 8));
            }
        }

        return Convert.ToBase64String(packed);
    }

    private static string ReadLegacyId(JsonElement idElement) => idElement.ValueKind switch
    {
        JsonValueKind.String => NormalizeLegacyId(idElement.GetString() ?? string.Empty),
        JsonValueKind.Number => NormalizeLegacyId(idElement.GetRawText()),
        _ => throw new InvalidOperationException("Legacy save is missing a valid entity id.")
    };

    private static string ReadLegacyId(string value) => NormalizeLegacyId(value);

    private static string NormalizeLegacyId(string raw)
    {
        if (Guid.TryParse(raw, out var guid))
        {
            return guid.ToString("N");
        }

        return CreateStableEntityId($"legacy:{raw}");
    }

    private static string CreateStableEntityId(string seed)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash).ToString("N");
    }

    private static int GetRequiredInt(JsonElement element, string propertyName) =>
        TryGetInt(element, propertyName) ?? throw new InvalidOperationException($"Missing required integer property '{propertyName}'.");

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException($"Missing required string property '{propertyName}'.");
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static bool? TryGetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static DateTime ReadDateTime(JsonElement element, string propertyName)
    {
        var value = GetRequiredString(element, propertyName);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : throw new InvalidOperationException($"Invalid date value for '{propertyName}'.");
    }
}
