using System;
using System.IO;
using System.Text.Json.Nodes;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class IntegrityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.Scheduler preserves order-zero actor across adversarial re-registration", PreservesOrderZeroActor);
        registry.Add("Persistence.SaveMigrator upgrades version 15 saves and re-derives ambiguous zero orders", MigratesVersionFifteenSaves);
        registry.Add("Persistence.SaveMigrator upgrades version 16 Warlord state without replaying bonus", MigratesVersionSixteenWarlordState);
        registry.Add("Persistence.MetaProgression survives corrupt JSON payloads", MetaProgressionSurvivesCorruptJson);
        registry.Add("Persistence.MetaProgression upgrades unversioned files and stamps the schema version", MetaProgressionUpgradesUnversionedFiles);
        registry.Add("Persistence.MetaProgression file load survives corruption on disk", MetaProgressionFileLoadSurvivesCorruption);
        registry.Add("Persistence.Relics round-trip runtime hook state", PreservesRelicRuntimeState);
        registry.Add("Persistence.Relics default runtime hook state for legacy payloads", DefaultsRelicRuntimeStateForLegacyPayloads);
        registry.Add("Persistence.SaveValidator accepts speed-zero shrines, npcs, and merchants", AcceptsSpeedZeroStaticObjects);
        registry.Add("Persistence.SaveValidator rejects negative component payload values", RejectsNegativeComponentPayloads);
    }

    private static void PreservesOrderZeroActor()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(6, 6, playerEnergy: 0);
        var alpha = CreateEntity("Alpha", new Position(1, 0), Faction.Enemy, energy: 1200);
        var beta = CreateEntity("Beta", new Position(2, 0), Faction.Enemy, energy: 1200);
        var gamma = CreateEntity("Gamma", new Position(3, 0), Faction.Enemy, energy: 1200);
        world.AddEntity(alpha);
        world.AddEntity(beta);
        world.AddEntity(gamma);

        var scheduler = new TurnScheduler();
        scheduler.AttachWorld(world);
        scheduler.Register(alpha);
        scheduler.Register(beta);
        scheduler.Register(gamma);
        Expect.Equal(0, scheduler.GetOrder(alpha.Id), "First registered actor should hold order zero.");
        var beforeSave = scheduler.GetNextActor();
        Expect.Equal(alpha.Id, beforeSave!.Id, "Order-zero actor should win the energy tie before save.");

        world.SchedulerOrders.Clear();
        foreach (var entity in world.Entities)
        {
            world.SchedulerOrders[entity.Id] = scheduler.GetOrder(entity.Id);
        }

        world.SchedulerNextOrder = scheduler.NextOrder;

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with an order-zero actor.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again.");

        Expect.True(restored!.SchedulerOrders.TryGetValue(alpha.Id, out var restoredAlphaOrder), "Order-zero entry must survive the round-trip instead of being dropped.");
        Expect.Equal(0, restoredAlphaOrder, "Order-zero actor should restore with order zero.");

        var restoredScheduler = new TurnScheduler();
        restoredScheduler.AttachWorld(restored);
        restoredScheduler.NextOrder = restored.SchedulerNextOrder;

        // Adversarial: re-register in the reverse of the original registration order.
        restoredScheduler.Register(restored.GetEntity(gamma.Id)!);
        restoredScheduler.Register(restored.GetEntity(beta.Id)!);
        restoredScheduler.Register(restored.GetEntity(alpha.Id)!);

        Expect.Equal(0, restoredScheduler.GetOrder(alpha.Id), "Alpha should keep order zero after adversarial re-registration.");
        Expect.Equal(1, restoredScheduler.GetOrder(beta.Id), "Beta should keep order one after adversarial re-registration.");
        Expect.Equal(2, restoredScheduler.GetOrder(gamma.Id), "Gamma should keep order two after adversarial re-registration.");

        var afterLoad = restoredScheduler.GetNextActor();
        Expect.NotNull(afterLoad, "Restored scheduler should have a ready actor.");
        Expect.Equal(alpha.Id, afterLoad!.Id, "Order-zero actor should still win the energy tie after load.");
    }

    private static void MigratesVersionFifteenSaves()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var enemy = CreateEntity("Rat", new Position(1, 0), Faction.Enemy);
        world.AddEntity(enemy);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Setup save should succeed.");

        var path = Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["version"] = 15;
        foreach (var entity in root["floors"]![0]!["entities"]!.AsArray())
        {
            // Version 15 wrote 0 both for unscheduled entities and the first actor.
            var isEnemy = string.Equals(entity!["id"]!.GetValue<string>(), enemy.Id.Value.ToString("N"), StringComparison.OrdinalIgnoreCase);
            entity["schedulerOrder"] = isEnemy ? 1 : 0;
        }

        File.WriteAllText(path, root.ToJsonString());

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Version 15 saves should still produce metadata.");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated v15 save should report the current normalized version.");

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Version 15 saves should still load.");
        Expect.False(restored!.SchedulerOrders.ContainsKey(world.Player.Id), "Ambiguous legacy zero orders should be re-derived, not restored.");
        Expect.True(restored.SchedulerOrders.TryGetValue(enemy.Id, out var enemyOrder) && enemyOrder == 1, "Positive legacy orders should be preserved by migration.");
    }

    private static void MigratesVersionSixteenWarlordState()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        world.Depth = 3;
        world.Player.Stats.Attack = 14;
        var relic = new RelicComponent();
        relic.RelicIds.Add("warlord_crest");
        relic.AppliedStatTotals["warlord_crest"] = 4;
        world.Player.SetComponent(relic);
        world.SchedulerOrders[world.Player.Id] = 0;
        world.SchedulerNextOrder = 1;

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Setup save should succeed.");
        var path = Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["version"] = 16;
        RemoveAppliedStatTotals(root["entities"]!.AsArray());
        foreach (var floor in root["floors"]!.AsArray())
        {
            RemoveAppliedStatTotals(floor!["entities"]!.AsArray());
        }

        File.WriteAllText(path, root.ToJsonString());

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Version 16 Warlord saves should migrate.");
        Expect.Equal(14, restored!.Player.Stats.Attack, "Migration must preserve aggregate attack exactly.");
        var restoredRelic = restored.Player.GetComponent<RelicComponent>()!;
        Expect.Equal(4, restoredRelic.AppliedStatTotals["warlord_crest"], "Migration should seed the depth-derived Warlord baseline.");
        Expect.True(restored.SchedulerOrders.TryGetValue(restored.Player.Id, out var schedulerOrder), "Version 16 valid order zero must remain present.");
        Expect.Equal(0, schedulerOrder, "Version 16 migration must not apply legacy scheduler-zero normalization.");

        RelicProcessor.ProcessHook("on_floor_enter", restored.Player, restored, restored.ContentDatabase, new RelicHookContext());
        Expect.Equal(14, restored.Player.Stats.Attack, "Reentering the migrated depth must not replay Warlord bonus.");
        restored.Depth = 4;
        RelicProcessor.ProcessHook("on_floor_enter", restored.Player, restored, restored.ContentDatabase, new RelicHookContext());
        Expect.Equal(16, restored.Player.Stats.Attack, "A deeper floor should grant only the missing Warlord delta after migration.");
    }

    private static void RemoveAppliedStatTotals(JsonArray entities)
    {
        foreach (var entity in entities)
        {
            if (entity!["relic"] is JsonObject payload)
            {
                payload.Remove("appliedStatTotals");
            }
        }
    }

    private static void MetaProgressionSurvivesCorruptJson()
    {
        foreach (var payload in new[] { "{ definitely not json", "[1,2,3]", "null", "\"a string\"", "42", "{\"echoesTotal\": }" })
        {
            var data = MetaProgressionData.FromJson(payload);
            Expect.NotNull(data, $"Corrupt payload '{payload}' should yield a fresh instance.");
            Expect.Equal(0, data.EchoesTotal, $"Corrupt payload '{payload}' should reset to defaults.");
            Expect.Equal(MetaProgressionData.CurrentVersion, data.Version, $"Corrupt payload '{payload}' should stamp the current schema version.");
        }

        var truncated = MetaProgressionData.FromJson("{\"echoesTotal\": 50, \"runHist");
        Expect.Equal(0, truncated.EchoesTotal, "Truncated payloads should reset to defaults instead of throwing.");

        var nullCollections = MetaProgressionData.FromJson("{\"echoesTotal\": 10, \"unlockLevels\": null, \"runHistory\": null}");
        Expect.Equal(10, nullCollections.EchoesTotal, "Valid fields should survive when collections are null.");
        Expect.Equal(0, nullCollections.UnlockLevels.Count, "Null unlock levels should default to an empty dictionary.");
        Expect.Equal(0, nullCollections.RunHistory.Count, "Null run history should default to an empty list.");

        var negative = MetaProgressionData.FromJson("{\"echoesTotal\": -5, \"echoesSpent\": -3, \"unlockLevels\": {\"hp\": -2}}");
        Expect.Equal(0, negative.EchoesTotal, "Negative echo totals should clamp to zero.");
        Expect.Equal(0, negative.EchoesAvailable, "Negative echo state should never yield negative available echoes.");
        Expect.Equal(0, negative.UnlockLevels["hp"], "Negative unlock levels should clamp to zero.");
    }

    private static void MetaProgressionUpgradesUnversionedFiles()
    {
        var legacyJson = "{\"echoesTotal\": 40, \"echoesSpent\": 15, \"hasCompletedFirstClear\": true, \"ascensionLevel\": 2, \"unlockLevels\": {\"max_hp\": 3}}";
        var data = MetaProgressionData.FromJson(legacyJson);

        Expect.Equal(MetaProgressionData.CurrentVersion, data.Version, "Unversioned files should upgrade to the current schema version.");
        Expect.Equal(40, data.EchoesTotal, "Unversioned echo totals should survive the upgrade.");
        Expect.Equal(25, data.EchoesAvailable, "Unversioned echo spend should survive the upgrade.");
        Expect.Equal(2, data.AscensionLevel, "Unversioned ascension level should survive the upgrade.");
        Expect.Equal(3, data.UnlockLevels["max_hp"], "Unversioned unlock levels should survive the upgrade.");

        var roundTripped = MetaProgressionData.FromJson(data.ToJson());
        Expect.Equal(MetaProgressionData.CurrentVersion, roundTripped.Version, "Round-tripped data should carry the schema version.");
        Expect.Equal(40, roundTripped.EchoesTotal, "Round-tripped data should preserve echo totals.");
    }

    private static void MetaProgressionFileLoadSurvivesCorruption()
    {
        using var sandbox = IntegritySandbox.Create();
        var path = Path.Combine(sandbox.DirectoryPath, "meta_progression.json");
        File.WriteAllText(path, "{\"echoesTotal\": 12, \"unlock");

        var data = MetaProgressionData.LoadFromFile(path);
        Expect.NotNull(data, "Corrupt files should yield a fresh instance.");
        Expect.Equal(0, data.EchoesTotal, "Corrupt files should reset to defaults.");

        // A fresh instance must be safe to save back over the corrupt file.
        data.EchoesTotal = 7;
        data.SaveToFile(path);
        var reloaded = MetaProgressionData.LoadFromFile(path);
        Expect.Equal(7, reloaded.EchoesTotal, "Recovered data should round-trip through the same file.");
        Expect.Equal(MetaProgressionData.CurrentVersion, reloaded.Version, "Recovered data should be versioned on disk.");
    }

    private static void PreservesRelicRuntimeState()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(6, 6);
        var enemyA = CreateEntity("Rat", new Position(1, 0), Faction.Enemy);
        var enemyB = CreateEntity("Bat", new Position(2, 0), Faction.Enemy);
        world.AddEntity(enemyA);
        world.AddEntity(enemyB);

        var relic = new RelicComponent
        {
            ShieldCharges = 2,
            LowHpRelicFired = true,
            DamageBuffPercent = 20,
            DamageBuffExpiresOnTurn = 42,
            LastMerchantDiscountDepth = 3,
        };
        relic.RelicIds.Add("death_mask");
        relic.RelicIds.Add("predator_mark");
        relic.FirstHitEntityIds.Add(enemyA.Id);
        relic.FirstHitEntityIds.Add(enemyB.Id);
        relic.AppliedOneTimeRelics.Add("cursed_blade");
        relic.AppliedOneTimeRelics.Add("glass_cannon");
        relic.AppliedStatTotals["warlord_crest"] = 4;
        relic.AppliedStatTotals["a_relic"] = 2;
        world.Player.SetComponent(relic);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should persist relic runtime state.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again.");

        var restoredRelic = restored!.Player.GetComponent<RelicComponent>();
        Expect.NotNull(restoredRelic, "Relic component should survive round-trip.");
        Expect.Equal(2, restoredRelic!.ShieldCharges, "Shield charges should survive round-trip.");
        Expect.True(restoredRelic.LowHpRelicFired, "One-shot low HP guard should survive round-trip.");
        Expect.Equal(20, restoredRelic.DamageBuffPercent, "Death mask damage buff percent should survive round-trip.");
        Expect.Equal(42, restoredRelic.DamageBuffExpiresOnTurn, "Damage buff expiry turn should survive round-trip.");
        Expect.Equal(3, restoredRelic.LastMerchantDiscountDepth, "Merchant discount depth should survive round-trip.");
        Expect.Equal(2, restoredRelic.FirstHitEntityIds.Count, "First-hit tracking should survive round-trip.");
        Expect.True(restoredRelic.FirstHitEntityIds.Contains(enemyA.Id), "First-hit entity ids should survive round-trip.");
        Expect.True(restoredRelic.FirstHitEntityIds.Contains(enemyB.Id), "All first-hit entity ids should survive round-trip.");
        Expect.Equal(2, restoredRelic.AppliedOneTimeRelics.Count, "One-time relic tracking should survive round-trip.");
        Expect.True(restoredRelic.AppliedOneTimeRelics.Contains("cursed_blade"), "One-time relic ids should survive round-trip.");
        Expect.True(restoredRelic.AppliedOneTimeRelics.Contains("glass_cannon"), "All one-time relic ids should survive round-trip.");
        Expect.Equal(4, restoredRelic.AppliedStatTotals["warlord_crest"], "Warlord applied stat total should survive round-trip.");
        Expect.Equal(2, restoredRelic.AppliedStatTotals["a_relic"], "Generic applied stat totals should survive round-trip.");
    }

    private static void DefaultsRelicRuntimeStateForLegacyPayloads()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var relic = new RelicComponent { ShieldCharges = 1 };
        relic.RelicIds.Add("vampire_fang");
        world.Player.SetComponent(relic);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Setup save should succeed.");

        // Simulate a save written before the runtime hook fields existed by
        // stripping the new properties from the relic payload.
        var path = Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var stripped = 0;
        foreach (var entity in root["floors"]![0]!["entities"]!.AsArray())
        {
            if (entity!["relic"] is JsonObject payload)
            {
                foreach (var property in new[] { "firstHitEntityIds", "appliedOneTimeRelics", "appliedStatTotals", "damageBuffPercent", "damageBuffExpiresOnTurn", "lastMerchantDiscountDepth" })
                {
                    Expect.True(payload.Remove(property), $"Baseline relic payload should contain '{property}'.");
                    stripped++;
                }
            }
        }

        Expect.Equal(6, stripped, "All new relic properties should be stripped from the payload.");
        File.WriteAllText(path, root.ToJsonString());

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Legacy-shaped relic payloads should still load.");
        var restoredRelic = restored!.Player.GetComponent<RelicComponent>()!;
        Expect.Equal(1, restoredRelic.ShieldCharges, "Existing relic fields should still rehydrate.");
        Expect.Equal(0, restoredRelic.FirstHitEntityIds.Count, "Missing first-hit state should default to empty.");
        Expect.Equal(0, restoredRelic.AppliedOneTimeRelics.Count, "Missing one-time relic state should default to empty.");
        Expect.Equal(0, restoredRelic.AppliedStatTotals.Count, "Missing applied stat totals should default to empty.");
        Expect.Equal(0, restoredRelic.DamageBuffPercent, "Missing damage buff percent should default to zero.");
        Expect.Equal(0, restoredRelic.DamageBuffExpiresOnTurn, "Missing damage buff expiry should default to zero.");
        Expect.Equal(-1, restoredRelic.LastMerchantDiscountDepth, "Missing merchant discount depth should default to -1.");
    }

    private static void AcceptsSpeedZeroStaticObjects()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(6, 6);

        var shrine = CreateEntity("Shrine", new Position(1, 1), Faction.Neutral, speed: 0, blocksMovement: false);
        shrine.SetComponent(new ShrineComponent { ShrineType = "healing", HPCost = 10 });
        world.AddEntity(shrine);

        var npc = CreateEntity("Elder", new Position(2, 1), Faction.Neutral, speed: 0, blocksMovement: false);
        npc.SetComponent(new NpcComponent { TemplateId = "elder", Role = "quest", DialogueId = "intro" });
        world.AddEntity(npc);

        var merchant = CreateEntity("Merchant", new Position(3, 1), Faction.Neutral, speed: 0, blocksMovement: false);
        merchant.SetComponent(new MerchantComponent(Array.Empty<MerchantOfferState>()));
        world.AddEntity(merchant);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should accept speed-zero shrines, npcs, and merchants.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "World with speed-zero static objects should load again.");
        Expect.NotNull(restored!.GetEntity(shrine.Id)?.GetComponent<ShrineComponent>(), "Shrine should survive the round-trip.");
        Expect.NotNull(restored.GetEntity(npc.Id)?.GetComponent<NpcComponent>(), "Npc should survive the round-trip.");
        Expect.NotNull(restored.GetEntity(merchant.Id)?.GetComponent<MerchantComponent>(), "Merchant should survive the round-trip.");
    }

    private static void RejectsNegativeComponentPayloads()
    {
        using var sandbox = IntegritySandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(6, 6);
        var relic = new RelicComponent { ShieldCharges = 2 };
        relic.RelicIds.Add("vampire_fang");
        world.Player.SetComponent(relic);
        world.Player.SetComponent(new WalletComponent { Gold = 25 });
        world.Player.SetComponent(new ProgressionComponent { Level = 2, Experience = 30, ExperienceToNextLevel = 80 });
        world.Player.SetComponent(new KillStreakComponent { CurrentStreak = 1, HighestStreak = 2, BonusXpAwarded = 1 });

        var shrine = CreateEntity("Shrine", new Position(1, 1), Faction.Neutral, speed: 0, blocksMovement: false);
        shrine.SetComponent(new ShrineComponent { ShrineType = "combat", HPCost = 5 });
        world.AddEntity(shrine);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Baseline save should be valid.");
        var path = Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1));
        var originalJson = File.ReadAllText(path);
        Expect.NotNull(manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult(), "Baseline save should load.");

        var mutations = new (string Description, string Component, string Property, JsonNode Value)[]
        {
            ("negative relic shield charges", "relic", "shieldCharges", -1),
            ("blank relic id", "relic", "relicIds", new JsonArray("  ")),
            ("negative applied relic stat total", "relic", "appliedStatTotals", new JsonObject { ["warlord_crest"] = -1 }),
            ("blank applied relic stat id", "relic", "appliedStatTotals", new JsonObject { [" "] = 2 }),
            ("over-cap Warlord stat total", "relic", "appliedStatTotals", new JsonObject { ["warlord_crest"] = 11 }),
            ("case-colliding applied relic ids", "relic", "appliedStatTotals", new JsonObject { ["warlord_crest"] = 2, ["Warlord_Crest"] = 4 }),
            ("negative wallet gold", "wallet", "gold", -5),
            ("negative progression experience", "progression", "experience", -1),
            ("invalid progression level", "progression", "level", 0),
            ("negative kill streak", "killStreak", "currentStreak", -3),
            ("negative shrine HP cost", "shrine", "hpCost", -1),
            ("blank shrine type", "shrine", "shrineType", ""),
        };

        foreach (var (description, component, property, value) in mutations)
        {
            var root = JsonNode.Parse(originalJson)!.AsObject();
            var mutatedCount = 0;
            foreach (var entity in root["floors"]![0]!["entities"]!.AsArray())
            {
                if (entity![component] is JsonObject payload)
                {
                    Expect.True(payload.ContainsKey(property), $"Baseline save should expose '{component}.{property}' for mutation ({description}).");
                    payload[property] = value.DeepClone();
                    mutatedCount++;
                }
            }

            Expect.True(mutatedCount > 0, $"Mutation '{description}' should target at least one entity payload.");
            File.WriteAllText(path, root.ToJsonString());
            Expect.True(manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult() is null, $"Save with {description} should fail validation.");
            File.WriteAllText(path, originalJson);
        }

        Expect.NotNull(manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult(), "Restored baseline save should load again.");
    }

    private static WorldState CreateWorld(int width, int height, int playerEnergy = 1000)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 4242;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = CreateEntity("Hero", Position.Zero, Faction.Player, energy: playerEnergy);
        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private static Entity CreateEntity(string name, Position position, Faction faction, int speed = 100, bool blocksMovement = true, int energy = 1000)
    {
        return new Entity(
            name,
            position,
            new Stats { HP = 20, MaxHP = 20, Attack = 10, Accuracy = 10, Defense = 1, Evasion = 0, Speed = speed, ViewRadius = 8, Energy = energy },
            faction,
            blocksMovement,
            id: EntityId.New());
    }

    private sealed class IntegritySandbox : IDisposable
    {
        private IntegritySandbox(string directoryPath, DateTime timestamp)
        {
            DirectoryPath = directoryPath;
            Timestamp = timestamp;
        }

        public string DirectoryPath { get; }

        private DateTime Timestamp { get; }

        public Func<DateTime> Clock => () => Timestamp;

        public static IntegritySandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-integrity-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new IntegritySandbox(directoryPath, new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc));
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
