using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class FloorClearPersistenceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("FloorClear.Persistence round-trips rewarded depths", RoundTrip);
        registry.Add("FloorClear.Persistence rejects malformed rewarded depths", RejectsMalformed);
        registry.Add("FloorClear.Persistence migrates every legacy version conservatively", MigratesLegacy);
        registry.Add("FloorClear.Persistence v17 migration preserves scheduler and relic state", PreservesVersion17);
    }

    private static WorldState World(int depth, bool player)
    {
        var world = new WorldState { Seed = 42, Depth = depth };
        world.InitGrid(4, 4);
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                world.SetTile(new Position(x, y), TileType.Floor);
        if (player)
        {
            world.Player = new Entity("Hero", new Position(0, 0), new Stats { HP = 20, MaxHP = 20, Speed = 100 }, Faction.Player);
            world.AddEntity(world.Player);
        }
        return world;
    }

    private static void RoundTrip()
    {
        WithSave((manager, path) =>
        {
            var active = World(2, true);
            var cached = World(1, false);
            var rewards = new List<int> { 2, 1 };
            var snapshot = new SaveRunSnapshot(42, 2, active, new Dictionary<int, WorldState> { [1] = cached, [2] = active }, rewardedFloorDepths: rewards);
            rewards.Clear();
            Expect.True(manager.SaveRun(snapshot, 1).GetAwaiter().GetResult(), "Rewarded run should save");
            var loaded = manager.LoadRun(1).GetAwaiter().GetResult();
            Expect.NotNull(loaded, "Rewarded run should load");
            Expect.True(loaded!.RewardedFloorDepths.SequenceEqual(new[] { 1, 2 }), "Snapshot should copy rewards and serialization should sort them");
            Expect.Equal(18, (int)JsonNode.Parse(File.ReadAllText(path))!["version"]!, "Save schema should be version 18");
        });
    }

    private static void RejectsMalformed()
    {
        WithSave((manager, path) =>
        {
            Expect.True(manager.SaveGame(World(0, true), 1).GetAwaiter().GetResult(), "Fixture should save");
            var json = File.ReadAllText(path);
            foreach (var invalid in new[] { "null", "[-1]", "[0,0]", "[99]", "[\"zero\"]" })
            {
                var root = JsonNode.Parse(json)!;
                root["rewardedFloorDepths"] = JsonNode.Parse(invalid);
                File.WriteAllText(path, root.ToJsonString());
                Expect.True(manager.LoadRun(1).GetAwaiter().GetResult() is null, $"Reject malformed reward state {invalid}");
                Expect.True(manager.GetSaveMetadata(1) is null, "Malformed rewards should not expose slot metadata");
            }
        });
    }

    private static void MigratesLegacy()
    {
        WithSave((manager, path) =>
        {
            for (var version = 1; version <= 17; version++)
                foreach (var hostileAlive in new[] { false, true })
                {
                    var world = World(0, true);
                    world.AddEntity(new Entity("Enemy", new Position(1, 0), new Stats { HP = hostileAlive ? 5 : 0, MaxHP = 5, Speed = 100 }, Faction.Enemy));
                    // Living neutrals never prevent the conservative already-rewarded inference.
                    world.AddEntity(new Entity("Neutral", new Position(2, 0), new Stats { HP = 5, MaxHP = 5, Speed = 100 }, Faction.Neutral));
                    Expect.True(manager.SaveGame(world, 1).GetAwaiter().GetResult(), "Legacy fixture should save");
                    var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                    root["version"] = version;
                    root.Remove("rewardedFloorDepths");
                    if (version <= 2)
                    {
                        root.Remove("floors");
                        root["explored"] = Convert.ToBase64String(new byte[16]);
                        root["visible"] = Convert.ToBase64String(new byte[16]);
                    }
                    if (version == 1)
                    {
                        root["timestamp"] = "2026-07-01T00:00:00Z";
                        root["mapWidth"] = 4;
                        root["mapHeight"] = 4;
                        root["currentFloor"] = 0;
                        var legacyEntities = new JsonArray();
                        foreach (var entity in world.Entities)
                        {
                            var legacy = new JsonObject
                            {
                                ["id"] = entity == world.Player ? 1 : entity.Faction == Faction.Enemy ? 2 : 3,
                                ["name"] = entity.Name,
                                ["posX"] = entity.Position.X,
                                ["posY"] = entity.Position.Y,
                                ["faction"] = (int)entity.Faction,
                                ["hp"] = entity.Stats.HP,
                                ["maxHP"] = entity.Stats.MaxHP,
                                ["attack"] = 0,
                                ["defense"] = 0,
                                ["speed"] = 100,
                                ["viewRadius"] = 4,
                            };
                            if (entity == world.Player) root["player"] = legacy;
                            else legacyEntities.Add(legacy);
                        }
                        root["entities"] = legacyEntities;
                    }
                    File.WriteAllText(path, root.ToJsonString());
                    var loaded = manager.LoadRun(1).GetAwaiter().GetResult();
                    Expect.NotNull(loaded, $"Version {version} should migrate");
                    Expect.Equal(!hostileAlive, loaded!.RewardedFloorDepths.Contains(0), $"Version {version} should infer rewards from living hostiles only");
                }
        });
    }

    private static void PreservesVersion17()
    {
        WithSave((manager, path) =>
        {
            var active = World(2, true);
            active.SchedulerOrders[active.Player.Id] = 0;
            active.SchedulerNextOrder = 7;
            var relic = new RelicComponent { ShieldCharges = 2, DamageBuffPercent = 20, DamageBuffExpiresOnTurn = 30, LastMerchantDiscountDepth = 2 };
            relic.RelicIds.Add("warlord_crest");
            relic.AppliedStatTotals["warlord_crest"] = 6;
            relic.AppliedOneTimeRelics.Add("glass_cannon");
            relic.FirstHitEntityIds.Add(active.Player.Id);
            active.Player.SetComponent(relic);
            var cached = World(1, false);
            cached.AddEntity(new Entity("Enemy", new Position(1, 0), new Stats { HP = 5, MaxHP = 5, Speed = 100 }, Faction.Enemy));
            Expect.True(manager.SaveRun(new SaveRunSnapshot(42, 2, active, new Dictionary<int, WorldState> { [1] = cached, [2] = active }), 1).GetAwaiter().GetResult(), "Fixture should save");
            var root = JsonNode.Parse(File.ReadAllText(path))!;
            var floorsBefore = root["floors"]!.ToJsonString();
            root["version"] = 17;
            root.AsObject().Remove("rewardedFloorDepths");
            File.WriteAllText(path, root.ToJsonString());
            var loaded = manager.LoadRun(1).GetAwaiter().GetResult();
            Expect.NotNull(loaded, "v17 should load");
            Expect.True(loaded!.RewardedFloorDepths.SequenceEqual(new[] { 2 }), "Only empty active floor should be claimed, cached hostile floor stays eligible");
            Expect.True(manager.SaveRun(loaded, 1).GetAwaiter().GetResult(), "Migrated snapshot should save");
            Expect.Equal(floorsBefore, JsonNode.Parse(File.ReadAllText(path))!["floors"]!.ToJsonString(), "v17 migration must preserve all floor payloads including scheduler zero and relic state");
        });
    }

    private static void WithSave(Action<SaveManager, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "floor-clear-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(new SaveManager(directory), Path.Combine(directory, SaveSlots.GetFileName(1))); }
        finally { Directory.Delete(directory, true); }
    }
}
