using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class SaveManagerTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.SaveManager round-trips full world state", RoundTripRestoresWorldState);
        registry.Add("Persistence.SaveManager exposes save metadata", MetadataIsAvailableAfterSave);
        registry.Add("Persistence.SaveManager supports autosave slot", AutosaveSlotUsesDedicatedPath);
    }

    private static void RoundTripRestoresWorldState()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var original = CreateWorld();

        var saved = manager.SaveGame(original, SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.True(saved, "SaveManager should persist a valid world");

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "SaveManager should load the saved world");
        Expect.Equal(WorldSignature(original), WorldSignature(restored!), "Loaded world should match the saved world signature");
    }

    private static void MetadataIsAvailableAfterSave()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld();

        Expect.True(manager.SaveGame(world, SaveSlots.Slot2).GetAwaiter().GetResult(), "SaveManager should save before metadata is queried");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot2);
        Expect.NotNull(metadata, "Metadata should be returned for a valid save");
        Expect.Equal(SaveSlots.Slot2, metadata!.SlotIndex, "Metadata should report the requested slot");
        Expect.Equal(world.Depth, metadata.Depth, "Metadata should reflect world depth");
        Expect.Equal(world.TurnNumber, metadata.TurnNumber, "Metadata should reflect turn number");
        Expect.Equal(world.Player.Name, metadata.PlayerName, "Metadata should include the player name");
        Expect.Equal(sandbox.Timestamp, metadata.SavedAt, "Metadata should use the save timestamp");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata.Version, "Metadata should expose the normalized save version");
    }

    private static void AutosaveSlotUsesDedicatedPath()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld();

        Expect.True(manager.SaveGame(world, SaveSlots.Autosave).GetAwaiter().GetResult(), "Autosave slot should save successfully");
        Expect.True(manager.HasSave(SaveSlots.Autosave), "Autosave slot should report an existing save");
        Expect.True(File.Exists(Path.Combine(sandbox.DirectoryPath, "autosave.json")), "Autosave should use the dedicated autosave file name");

        manager.DeleteSave(SaveSlots.Autosave);
        Expect.False(manager.HasSave(SaveSlots.Autosave), "Deleting the autosave slot should remove the save file");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(6, 5);
        world.Seed = 4242;
        world.Depth = 3;
        world.TurnNumber = 88;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(0, 0), TileType.Wall);
        world.SetTile(new Position(5, 4), TileType.StairsDown);
        world.SetTile(new Position(4, 2), TileType.Door);
        world.SetDoorOpen(new Position(4, 2), true);
        world.SetVisible(new Position(1, 1), true);
        world.SetVisible(new Position(2, 1), true);
        world.SetVisible(new Position(3, 3), true);
        world.ClearVisibility();
        world.SetVisible(new Position(2, 1), true);
        world.SetVisible(new Position(3, 3), true);

        var player = new Entity(
            "Hero",
            new Position(1, 1),
            new Stats { HP = 17, MaxHP = 20, Attack = 5, Accuracy = 7, Defense = 3, Evasion = 2, Speed = 110, ViewRadius = 9, Energy = 650 },
            Faction.Player,
            id: new EntityId(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var playerInventory = new InventoryComponent(4);
        var sword = new ItemInstance
        {
            InstanceId = new EntityId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            TemplateId = "iron_sword",
            CurrentCharges = 0,
            StackCount = 1,
            IsIdentified = true,
        };
        var potion = new ItemInstance
        {
            InstanceId = new EntityId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            TemplateId = "health_potion",
            CurrentCharges = 2,
            StackCount = 3,
            IsIdentified = false,
        };
        playerInventory.Add(sword);
        playerInventory.Add(potion);
        playerInventory.TryEquip(sword, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 2 }, out _);
        player.SetComponent(playerInventory);

        StatusEffectProcessor.ApplyEffect(player, StatusEffectType.Poisoned, 4, 2);

        var enemy = new Entity(
            "Skeleton",
            new Position(3, 1),
            new Stats { HP = 9, MaxHP = 12, Attack = 4, Accuracy = 3, Defense = 1, Evasion = 1, Speed = 90, ViewRadius = 7, Energy = 200 },
            Faction.Enemy,
            blocksSight: true,
            id: new EntityId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        world.DropItem(new Position(2, 2), new ItemInstance
        {
            InstanceId = new EntityId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            TemplateId = "town_portal",
            CurrentCharges = 1,
            StackCount = 1,
            IsIdentified = true,
        });

        return world;
    }

    private static string WorldSignature(WorldState world)
    {
        var parts = new List<string>
        {
            $"seed={world.Seed}",
            $"depth={world.Depth}",
            $"turn={world.TurnNumber}",
            $"size={world.Width}x{world.Height}",
            $"tiles={TileSignature(world)}",
            $"explored={FlagSignature(world, explored: true)}",
            $"visible={FlagSignature(world, explored: false)}",
            $"doors={DoorSignature(world)}",
            $"entities={EntitySignature(world)}",
            $"ground={GroundItemSignature(world)}",
            $"player={world.Player.Id.Value:N}",
        };

        return string.Join("|", parts);
    }

    private static string TileSignature(WorldState world)
    {
        var values = new char[world.Width * world.Height];
        var index = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                values[index++] = (char)('A' + (int)world.GetTile(new Position(x, y)));
            }
        }

        return new string(values);
    }

    private static string FlagSignature(WorldState world, bool explored)
    {
        var values = new char[world.Width * world.Height];
        var index = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var value = explored ? world.IsExplored(new Position(x, y)) : world.IsVisible(new Position(x, y));
                values[index++] = value ? '1' : '0';
            }
        }

        return new string(values);
    }

    private static string DoorSignature(WorldState world)
    {
        var values = new List<string>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) == TileType.Door)
                {
                    values.Add($"{x},{y},{(world.IsDoorOpen(position) ? 1 : 0)}");
                }
            }
        }

        return string.Join(";", values);
    }

    private static string EntitySignature(WorldState world)
    {
        return string.Join(";", world.Entities.Select(entity =>
        {
            var inventory = entity.GetComponent<InventoryComponent>();
            var statusEffects = entity.GetComponent<StatusEffectsComponent>();
            var inventorySignature = inventory is null
                ? "none"
                : $"cap={inventory.Capacity},items={string.Join(",", inventory.Items.Select(item => $"{item.InstanceId.Value:N}:{item.TemplateId}:{item.CurrentCharges}:{item.StackCount}:{item.IsIdentified}"))},equipped={string.Join(",", inventory.EquippedItems.OrderBy(pair => (int)pair.Key).Select(pair => $"{(int)pair.Key}:{pair.Value.Item.InstanceId.Value:N}:{string.Join("/", pair.Value.StatModifiers.OrderBy(modifier => modifier.Key).Select(modifier => $"{modifier.Key}={modifier.Value}"))}"))}";
            var statusSignature = statusEffects is null
                ? "none"
                : string.Join(",", statusEffects.Effects.Select(effect => $"{(int)effect.Type}:{effect.RemainingTurns}:{effect.Magnitude}"));

            return string.Join(",", new[]
            {
                entity.Id.Value.ToString("N"),
                entity.Name,
                entity.Position.X.ToString(),
                entity.Position.Y.ToString(),
                ((int)entity.Faction).ToString(),
                entity.BlocksMovement ? "1" : "0",
                entity.BlocksSight ? "1" : "0",
                entity.Stats.HP.ToString(),
                entity.Stats.MaxHP.ToString(),
                entity.Stats.Attack.ToString(),
                entity.Stats.Accuracy.ToString(),
                entity.Stats.Defense.ToString(),
                entity.Stats.Evasion.ToString(),
                entity.Stats.Speed.ToString(),
                entity.Stats.ViewRadius.ToString(),
                entity.Stats.Energy.ToString(),
                inventorySignature,
                statusSignature,
            });
        }));
    }

    private static string GroundItemSignature(WorldState world)
    {
        return string.Join(";", world.GetGroundItems()
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .SelectMany(pair => pair.Value.Select(item => $"{pair.Key.X},{pair.Key.Y},{item.InstanceId.Value:N},{item.TemplateId},{item.CurrentCharges},{item.StackCount},{item.IsIdentified}")));
    }

    private sealed class SaveSandbox : IDisposable
    {
        private SaveSandbox(string directoryPath, DateTime timestamp)
        {
            DirectoryPath = directoryPath;
            Timestamp = timestamp;
        }

        public string DirectoryPath { get; }

        public DateTime Timestamp { get; }

        public Func<DateTime> Clock => () => Timestamp;

        public static SaveSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-persistence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new SaveSandbox(directoryPath, new DateTime(2026, 3, 30, 12, 34, 56, DateTimeKind.Utc));
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }
}