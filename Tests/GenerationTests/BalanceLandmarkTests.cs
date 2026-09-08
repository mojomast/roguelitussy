using System;
using System.Linq;
using System.Text;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.GenerationTests;

public sealed class BalanceLandmarkTests : ITestSuite
{
    private static readonly int[] Seeds = Enumerable.Range(0, 64).ToArray();

    public void Register(TestRegistry registry)
    {
        registry.Add("Generation.Balance landmarks preserve placed shrine event identity", ShrineLandmarksPreservePlacedEventIdentity);
        registry.Add("Generation.Balance curse vaults emit authored chest details", CurseVaultsEmitAuthoredChestDetails);
        registry.Add("Generation.Balance safe floors include recovery caches", SafeFloorsIncludeRecoveryCaches);
        registry.Add("Generation.Balance boss landmarks remain guaranteed", BossLandmarksRemainGuaranteed);
        registry.Add("Generation.Balance requested landmarks always deliver or mark fallback", RequestedLandmarksDeliverOrFallback);
        registry.Add("Generation.Balance landmark detail signatures are deterministic", LandmarkDetailSignaturesAreDeterministic);
    }

    private static void ShrineLandmarksPreservePlacedEventIdentity()
    {
        var content = LoadContent();
        var foundRequestedShrine = false;

        foreach (var seed in Seeds)
        {
            var world = new WorldState { ContentDatabase = content };
            var level = new DungeonGenerator().GenerateLevel(world, seed, 1);
            var request = FloorEventResolver.ResolveFloorEvents(1, seed).SpecialRooms
                .SingleOrDefault(room => room.Type == SpecialRoomType.ShrineRoom);
            var shrineRooms = level.Rooms.Where(room => room.Tags?.Contains("shrine") == true).ToArray();
            var shrines = level.ShrineSpawns ?? Array.Empty<ShrineSpawnData>();

            if (request is null || shrineRooms.Length == 0)
            {
                Expect.Equal(0, shrines.Count, $"Seed {seed} should not emit a shrine when its shrine request was not placed.");
                continue;
            }

            foundRequestedShrine = true;
            Expect.Equal(1, shrines.Count, $"Seed {seed} should map the placed shrine prefab to one shrine detail.");
            var shrine = shrines[0];
            Expect.Equal(request.EventId, shrine.EventId, "Shrine detail should retain the floor-plan event id.");
            Expect.True(content.FloorEvents.ContainsKey(shrine.EventId), "Shrine detail should reference a valid content event.");
            Expect.True(shrineRooms.Any(room => Contains(room, shrine.Position)), "Shrine should be inside the placed shrine room.");
            Expect.True(LevelValidator.IsTraversable(world.GetTile(shrine.Position)), "Shrine position should be traversable.");
            Expect.False(shrine.Position == level.PlayerSpawn || shrine.Position == level.StairsDown, "Shrine should not overlap either stair.");
        }

        Expect.True(foundRequestedShrine, "Seed sweep should include at least one placed shrine request.");
    }

    private static void CurseVaultsEmitAuthoredChestDetails()
    {
        var content = LoadContent();
        var foundPlacedVault = false;

        foreach (var seed in Seeds)
        {
            var world = new WorldState { ContentDatabase = content };
            var level = new DungeonGenerator().GenerateLevel(world, seed, 2);
            var vaultRooms = level.Rooms.Where(room => room.PrefabId == "curse_vault").ToArray();
            var curseChests = (level.ChestSpawnDetails ?? Array.Empty<ChestSpawnData>())
                .Where(spawn => spawn.LootTableId == "curse_room_chest_loot")
                .ToArray();

            if (vaultRooms.Length == 0)
            {
                Expect.Equal(0, curseChests.Length, $"Seed {seed} should not emit curse chests when the vault was not placed.");
                continue;
            }

            foundPlacedVault = true;
            Expect.Equal(3, curseChests.Length, $"Seed {seed} vault should emit all three authored curse chest details.");
            Expect.True(curseChests.All(chest => vaultRooms.Any(room => Contains(room, chest.Position))), "Curse chests should remain inside the placed vault.");
            Expect.True(curseChests.All(chest => LevelValidator.IsTraversable(world.GetTile(chest.Position))), "Curse chest positions should be traversable.");
        }

        Expect.True(foundPlacedVault, "Seed sweep should include at least one placed curse vault.");
    }

    private static void SafeFloorsIncludeRecoveryCaches()
    {
        var content = LoadContent();
        foreach (var depth in new[] { 5, 10 })
        {
            foreach (var seed in Seeds)
            {
                var world = new WorldState { ContentDatabase = content };
                var level = new DungeonGenerator().GenerateLevel(world, seed, depth);
                var potions = (level.ItemSpawnDetails ?? Array.Empty<ItemSpawnData>())
                    .Where(spawn => spawn.TemplateId == "potion_health")
                    .ToArray();
                var caches = (level.ChestSpawnDetails ?? Array.Empty<ChestSpawnData>())
                    .Where(spawn => spawn.LootTableId == "safe_floor_merchant_stock")
                    .ToArray();

                Expect.Equal(FloorType.SafeFloor, level.FloorType, $"Depth {depth} should retain safe-floor metadata.");
                Expect.Equal(0, level.EnemySpawnDetails?.Count ?? 0, $"Safe depth {depth} seed {seed} should have no hostile spawns.");
                Expect.Equal(1, potions.Length, $"Safe depth {depth} seed {seed} should contain one fixed recovery potion.");
                Expect.Equal(1, caches.Length, $"Safe depth {depth} seed {seed} should contain one fixed merchant cache.");
                Expect.False(potions[0].Position == caches[0].Position, "Safe recovery details should occupy distinct tiles.");
                Expect.True(IsValidRecoveryPosition(world, level, potions[0].Position), "Recovery potion should use a valid nonstair tile.");
                Expect.True(IsValidRecoveryPosition(world, level, caches[0].Position), "Merchant cache should use a valid nonstair tile.");
            }
        }
    }

    private static void BossLandmarksRemainGuaranteed()
    {
        var content = LoadContent();
        foreach (var depth in new[] { 3, 6, 9 })
        {
            foreach (var seed in Seeds)
            {
                var world = new WorldState { ContentDatabase = content };
                var level = new DungeonGenerator().GenerateLevel(world, seed, depth);
                Expect.Equal(FloorType.BossFloor, level.FloorType, $"Depth {depth} should retain boss-floor metadata.");
                Expect.True((level.EnemySpawnDetails ?? Array.Empty<EnemySpawnData>()).Any(spawn => spawn.IsBoss), $"Boss depth {depth} seed {seed} should retain a boss spawn.");
            }
        }
    }

    private static void LandmarkDetailSignaturesAreDeterministic()
    {
        var content = LoadContent();
        foreach (var depth in new[] { 1, 2, 3, 5, 6, 9, 10 })
        {
            foreach (var seed in Seeds)
            {
                var first = Generate(content, seed, depth);
                var second = Generate(content, seed, depth);
                Expect.Equal(DetailSignature(first), DetailSignature(second), $"Depth {depth} seed {seed} should preserve landmark detail signatures.");
            }
        }
    }

    private static void RequestedLandmarksDeliverOrFallback()
    {
        var content = LoadContent();
        foreach (var depth in new[] { 1, 2, 4, 7, 8, 11 })
        {
            foreach (var seed in Seeds)
            {
                var world = new WorldState { ContentDatabase = content };
                var level = new DungeonGenerator().GenerateLevel(world, seed, depth);
                var requests = FloorEventResolver.ResolveFloorEvents(depth, seed).SpecialRooms;
                var landmarks = level.LandmarkSpawns ?? Array.Empty<LandmarkSpawnData>();

                Expect.Equal(requests.Count, landmarks.Count, $"Depth {depth} seed {seed} should deliver every requested landmark.");
                foreach (var request in requests)
                {
                    var landmark = landmarks.Single(item => item.Type == request.Type && item.EventId == request.EventId);
                    Expect.True(LevelValidator.FloodFill(world, level.PlayerSpawn, includeLockedDoors: true).Contains(landmark.Position), "Landmark should be reachable in the generated topology.");
                    if (request.Type == SpecialRoomType.ShrineRoom)
                    {
                        Expect.True((level.ShrineSpawns ?? Array.Empty<ShrineSpawnData>()).Any(spawn => spawn.Position == landmark.Position), "Shrine landmark should retain its runtime spawn detail.");
                    }
                    else if (request.Type == SpecialRoomType.CurseRoom)
                    {
                        Expect.True((level.ChestSpawnDetails ?? Array.Empty<ChestSpawnData>()).Any(spawn => spawn.Position == landmark.Position && spawn.LootTableId == "curse_room_chest_loot"), "Curse landmark should retain its runtime chest detail.");
                    }
                }
            }
        }
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        Expect.True(content.IsValid, "Baseline content should load for landmark generation tests.");
        return content;
    }

    private static LevelData Generate(ContentLoader content, int seed, int depth)
    {
        var world = new WorldState { ContentDatabase = content };
        return new DungeonGenerator().GenerateLevel(world, seed, depth);
    }

    private static bool IsValidRecoveryPosition(WorldState world, LevelData level, Position position) =>
        LevelValidator.IsTraversable(world.GetTile(position))
        && position != level.PlayerSpawn
        && position != level.StairsDown;

    private static bool Contains(RoomData room, Position position) =>
        position.X >= room.X && position.X < room.X + room.Width
        && position.Y >= room.Y && position.Y < room.Y + room.Height;

    private static string DetailSignature(LevelData level)
    {
        var builder = new StringBuilder();
        builder.Append(level.FloorType).Append('|');
        foreach (var room in level.Rooms)
        {
            builder.Append(room.PrefabId).Append(':').Append(room.X).Append(',').Append(room.Y).Append(';');
        }

        foreach (var spawn in level.EnemySpawnDetails ?? Array.Empty<EnemySpawnData>())
        {
            builder.Append(spawn.Position).Append(':').Append(spawn.TemplateId).Append(':').Append(spawn.IsBoss).Append(';');
        }

        foreach (var spawn in level.ChestSpawnDetails ?? Array.Empty<ChestSpawnData>())
        {
            builder.Append(spawn.Position).Append(':').Append(spawn.LootTableId).Append(';');
        }

        foreach (var spawn in level.ItemSpawnDetails ?? Array.Empty<ItemSpawnData>())
        {
            builder.Append(spawn.Position).Append(':').Append(spawn.TemplateId).Append(':').Append(spawn.QualityBonus).Append(';');
        }

        foreach (var spawn in level.TrapSpawnDetails ?? Array.Empty<TrapSpawnData>())
        {
            builder.Append(spawn.Position).Append(':').Append(spawn.TrapId).Append(';');
        }

        foreach (var spawn in level.ShrineSpawns ?? Array.Empty<ShrineSpawnData>())
        {
            builder.Append(spawn.Position).Append(':').Append(spawn.EventId).Append(';');
        }

        foreach (var landmark in level.LandmarkSpawns ?? Array.Empty<LandmarkSpawnData>())
        {
            builder.Append(landmark.Position).Append(':').Append(landmark.Type).Append(':').Append(landmark.EventId).Append(':').Append(landmark.IsFallback).Append(';');
        }

        return builder.ToString();
    }
}
