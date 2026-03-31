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
            SaveSerializer.CurrentVersion => JsonSerializer.Deserialize<SaveFileData>(json, SaveSerializer.JsonOptions)
                ?? throw new InvalidOperationException("Unable to deserialize save data."),
            _ => throw new InvalidOperationException($"Unsupported save version {version}.")
        };
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
        return data;
    }

    private static SaveFileData MigrateV2(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 2 save data.");

        data.Explored = ReencodeFlagsV2ToV3(data.Explored, data.Width, data.Height);
        data.Visible = ReencodeFlagsV2ToV3(data.Visible, data.Width, data.Height);
        data.Version = SaveSerializer.CurrentVersion;
        return data;
    }

    private static SaveFileData MigrateV3(JsonElement root)
    {
        var data = JsonSerializer.Deserialize<SaveFileData>(root.GetRawText(), SaveSerializer.JsonOptions)
            ?? throw new InvalidOperationException("Unable to deserialize version 3 save data.");

        data.Version = SaveSerializer.CurrentVersion;
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