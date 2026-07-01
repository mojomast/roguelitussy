using System;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class DeathResolverTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.DeathResolver killing rat drops rat loot and gold", KillingRatDropsRatLootAndGold);
        registry.Add("Simulation.DeathResolver loot roll is deterministic for same seed", LootRollIsDeterministicForSameSeed);
        registry.Add("Simulation.DeathResolver loot seed uses stable entity hash", LootSeedUsesStableEntityHash);
        registry.Add("Simulation.DeathResolver gold roll respects min max", GoldRollRespectsMinMax);
        registry.Add("Simulation.DeathResolver unattributed death drops loot but no gold", UnattributedDeathDropsLootButNoGold);
    }

    private static void KillingRatDropsRatLootAndGold()
    {
        var content = ContentLoader.LoadFromRepository();
        content.EnsureValid();

        var fixedRatId = EntityId.From("11111111-1111-1111-1111-111111111111");
        var turn = FindTurnThatDropsLoot(content, "rat", fixedRatId, seed: 42, depth: 1);

        var world = CreateWorld(seed: 42);
        world.ContentDatabase = content;
        world.Depth = 1;
        world.TurnNumber = turn;

        var player = CreateActor("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new WalletComponent { Gold = 0 });
        world.AddEntity(player);

        var rat = CreateEnemy("rat", new Position(2, 1), content, fixedRatId);
        rat.Stats.HP = 1;
        world.AddEntity(rat);

        var death = DeathResolver.ResolveKill(world, player, rat);

        Expect.True(death.Removed, "Killed rat should be removed from the world");
        Expect.True(death.DroppedItems.Count > 0, "Killed rat should drop loot");
        Expect.True(world.GetItemsAt(rat.Position).Count > 0, "Loot should appear on the ground at the corpse position");
        Expect.True(death.GoldAwarded > 0, "Killed rat should award gold");
        Expect.Equal(death.GoldAwarded, player.GetComponent<WalletComponent>()!.Gold, "Gold should be added to the killer's wallet");
    }

    private static void LootRollIsDeterministicForSameSeed()
    {
        var content = ContentLoader.LoadFromRepository();
        content.EnsureValid();

        var fixedRatId = EntityId.From("22222222-2222-2222-2222-222222222222");
        var turn = FindTurnThatDropsLoot(content, "rat", fixedRatId, seed: 7, depth: 2);

        DeathResolver.DeathResolution RunKill()
        {
            var world = CreateWorld(seed: 7);
            world.ContentDatabase = content;
            world.Depth = 2;
            world.TurnNumber = turn;

            var player = CreateActor("Player", new Position(1, 1), Faction.Player);
            player.SetComponent(new WalletComponent { Gold = 0 });
            world.AddEntity(player);

            var rat = CreateEnemy("rat", new Position(3, 4), content, fixedRatId);
            rat.Stats.HP = 1;
            world.AddEntity(rat);

            return DeathResolver.ResolveKill(world, player, rat);
        }

        var first = RunKill();
        var second = RunKill();

        Expect.Equal(first.DroppedItems.Count, second.DroppedItems.Count, "Same seed should produce the same number of drops");
        for (var i = 0; i < first.DroppedItems.Count; i++)
        {
            Expect.Equal(first.DroppedItems[i].TemplateId, second.DroppedItems[i].TemplateId, $"Drop {i} template should match");
            Expect.Equal(first.DroppedItems[i].StackCount, second.DroppedItems[i].StackCount, $"Drop {i} stack count should match");
        }
    }

    private static void LootSeedUsesStableEntityHash()
    {
        var entityId = EntityId.From("00112233-4455-6677-8899-aabbccddeeff");
        var position = new Position(5, 6);
        var method = typeof(DeathResolver).GetMethod("MixSeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Expect.NotNull(method, "DeathResolver seed mixer should remain available for deterministic seed regression coverage");

        var actual = (int)method!.Invoke(null, new object[] { 123, 2, position, 9, entityId })!;
        var expected = ExpectedMixSeed(123, 2, position, 9, entityId);

        Expect.Equal(expected, actual, "Death loot/gold seed should use a stable Guid byte hash instead of EntityId.GetHashCode");
    }

    private static void GoldRollRespectsMinMax()
    {
        var content = ContentLoader.LoadFromRepository();
        content.EnsureValid();
        content.TryGetEnemyTemplate("rat", out var ratTemplate);

        var fixedRatId = EntityId.From("33333333-3333-3333-3333-333333333333");
        var world = CreateWorld(seed: 99);
        world.ContentDatabase = content;
        world.Depth = 0;

        for (var turn = 0; turn < 50; turn++)
        {
            var testWorld = CreateWorld(seed: 99);
            testWorld.ContentDatabase = content;
            testWorld.Depth = 0;
            testWorld.TurnNumber = turn;

            var player = CreateActor("Player", new Position(1, 1), Faction.Player);
            player.SetComponent(new WalletComponent { Gold = 0 });
            testWorld.AddEntity(player);

            var rat = CreateEnemy("rat", new Position(2, 1), content, fixedRatId);
            rat.Stats.HP = 1;
            testWorld.AddEntity(rat);

            var death = DeathResolver.ResolveKill(testWorld, player, rat);
            Expect.True(death.GoldAwarded >= ratTemplate!.GoldMin, $"Turn {turn} gold should be >= {ratTemplate.GoldMin}");
            Expect.True(death.GoldAwarded <= ratTemplate.GoldMax, $"Turn {turn} gold should be <= {ratTemplate.GoldMax}");
        }
    }

    private static void UnattributedDeathDropsLootButNoGold()
    {
        var content = ContentLoader.LoadFromRepository();
        content.EnsureValid();

        var fixedRatId = EntityId.From("44444444-4444-4444-4444-444444444444");
        var turn = FindTurnThatDropsLoot(content, "rat", fixedRatId, seed: 5, depth: 0);

        var world = CreateWorld(seed: 5);
        world.ContentDatabase = content;
        world.Depth = 0;
        world.TurnNumber = turn;

        var rat = CreateEnemy("rat", new Position(2, 1), content, fixedRatId);
        rat.Stats.HP = 1;
        world.AddEntity(rat);

        var death = DeathResolver.ResolveUnattributedDeath(world, rat);

        Expect.True(death.Removed, "Unattributed death should still remove the victim");
        Expect.True(death.DroppedItems.Count > 0, "Unattributed death should still drop loot");
        Expect.Equal(0, death.GoldAwarded, "Unattributed death should not award gold");
    }

    private static int FindTurnThatDropsLoot(ContentLoader content, string enemyId, EntityId fixedId, int seed, int depth)
    {
        for (var turn = 0; turn < 10000; turn++)
        {
            var world = CreateWorld(seed);
            world.ContentDatabase = content;
            world.Depth = depth;
            world.TurnNumber = turn;

            var player = CreateActor("Player", new Position(1, 1), Faction.Player);
            world.AddEntity(player);

            var enemy = CreateEnemy(enemyId, new Position(2, 1), content, fixedId);
            enemy.Stats.HP = 1;
            world.AddEntity(enemy);

            var death = DeathResolver.ResolveKill(world, player, enemy);
            if (death.DroppedItems.Count > 0)
            {
                return turn;
            }
        }

        throw new InvalidOperationException($"Could not find a turn that makes '{enemyId}' drop loot.");
    }

    private static int ExpectedMixSeed(int seed, int depth, Position position, int turnNumber, EntityId entityId)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ depth;
            hash = (hash * 397) ^ position.X;
            hash = (hash * 397) ^ position.Y;
            hash = (hash * 397) ^ turnNumber;
            hash = (hash * 397) ^ ExpectedStableEntityIdHash(entityId);
            return hash;
        }
    }

    private static int ExpectedStableEntityIdHash(EntityId entityId)
    {
        Span<byte> bytes = stackalloc byte[16];
        entityId.Value.TryWriteBytes(bytes);

        unchecked
        {
            var hash = 17;
            foreach (var value in bytes)
            {
                hash = (hash * 31) + value;
            }

            return hash;
        }
    }

    private static WorldState CreateWorld(int seed = 123)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = seed;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction, Stats? stats = null)
    {
        var actor = new StubEntity(name, position, faction, stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        actor.SetComponent(new InventoryComponent());
        return actor;
    }

    private static Entity CreateEnemy(string templateId, Position position, ContentLoader content, EntityId? fixedId = null)
    {
        if (!content.TryGetEnemyTemplate(templateId, out var template))
        {
            throw new InvalidOperationException($"Unknown enemy template '{templateId}'.");
        }

        var enemy = new Entity(
            template.DisplayName,
            position,
            template.BaseStats.Clone(),
            template.Faction,
            id: fixedId ?? EntityId.New());
        enemy.SetComponent(new EnemyComponent { TemplateId = template.TemplateId });
        enemy.SetComponent(new XpValueComponent { Value = template.XpValue });
        return enemy;
    }
}
