using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class SynergyTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Synergy activates from relic and perk requirements", ActivatesFromRelicAndPerkRequirements);
        registry.Add("Simulation.Synergy detects one piece away potential synergies", DetectsPotentialSynergies);
        registry.Add("Simulation.Synergy requires item tags from inventory", RequiresItemTagsFromInventory);
        registry.Add("Simulation.Synergy passive stat application is idempotent", PassiveStatApplicationIsIdempotent);
        registry.Add("Simulation.Synergy passive bonus is removed when synergy deactivates", PassiveBonusRemovedWhenDeactivated);
        registry.Add("Simulation.Synergy authored damage bonus uses outgoing hook once", AuthoredDamageBonusUsesOutgoingHookOnce);
        registry.Add("Simulation.Synergy authored heal triggers once per enemy kill", AuthoredHealTriggersOncePerEnemyKill);
    }

    private static void PassiveBonusRemovedWhenDeactivated()
    {
        var content = new StubContentDatabase();
        var world = new WorldState();
        world.InitGrid(3, 3);
        var player = CreatePlayer();
        player.SetComponent(new RelicComponent());
        player.GetComponent<RelicComponent>()!.RelicIds.Add("vampire_fang");
        player.SetComponent(new ProgressionComponent());
        player.GetComponent<ProgressionComponent>()!.SelectedPerkIds.Add("perk_tough");

        SynergyResolver.ApplyPassiveSynergies(player, content, world);
        var boostedAttack = player.Stats.Attack;

        player.GetComponent<RelicComponent>()!.RelicIds.Remove("vampire_fang");
        SynergyResolver.ApplyPassiveSynergies(player, content, world);

        Expect.Equal(boostedAttack - 1, player.Stats.Attack, "Deactivated synergy should remove its stat bonus");
        Expect.False(
            player.GetComponent<SynergyComponent>()!.AppliedPassiveSynergyIds.Contains("stub_synergy"),
            "Applied passive list should drop the inactive synergy");
    }

    private static void ActivatesFromRelicAndPerkRequirements()
    {
        var content = new StubContentDatabase();
        var player = CreatePlayer();
        player.SetComponent(new RelicComponent());
        player.GetComponent<RelicComponent>()!.RelicIds.Add("vampire_fang");
        player.SetComponent(new ProgressionComponent());
        player.GetComponent<ProgressionComponent>()!.SelectedPerkIds.Add("perk_tough");

        var active = SynergyResolver.GetActiveSynergies(player, content);

        Expect.True(active.Exists(synergy => synergy.SynergyId == "stub_synergy"), "Relic plus perk should activate the authored synergy.");
    }

    private static void DetectsPotentialSynergies()
    {
        var content = new StubContentDatabase();
        var player = CreatePlayer();
        player.SetComponent(new RelicComponent());
        player.GetComponent<RelicComponent>()!.RelicIds.Add("vampire_fang");
        player.SetComponent(new ProgressionComponent());

        var potential = SynergyResolver.GetPotentialSynergies(player, content);

        Expect.True(potential.Exists(synergy => synergy.SynergyId == "stub_synergy"), "Missing only one perk should surface as a potential synergy.");
    }

    private static void RequiresItemTagsFromInventory()
    {
        var content = new StubContentDatabase();
        var player = CreatePlayer();
        var inventory = new InventoryComponent();
        inventory.Add(new ItemInstance { TemplateId = "shield_wooden" });
        player.SetComponent(inventory);

        var active = SynergyResolver.GetActiveSynergies(player, content);

        Expect.True(active.Exists(synergy => synergy.SynergyId == "shield_wall"), "Inventory item tags should satisfy synergy requirements.");
    }

    private static void PassiveStatApplicationIsIdempotent()
    {
        var content = new StubContentDatabase();
        var world = new WorldState();
        world.InitGrid(3, 3);
        var player = CreatePlayer();
        player.SetComponent(new RelicComponent());
        player.GetComponent<RelicComponent>()!.RelicIds.Add("vampire_fang");
        player.SetComponent(new ProgressionComponent());
        player.GetComponent<ProgressionComponent>()!.SelectedPerkIds.Add("perk_tough");

        SynergyResolver.ApplyPassiveSynergies(player, content, world);
        SynergyResolver.ApplyPassiveSynergies(player, content, world);

        Expect.Equal(6, player.Stats.Attack, "Passive stat synergies should apply once.");
    }

    private static void AuthoredDamageBonusUsesOutgoingHookOnce()
    {
        var content = ContentLoader.LoadFromRepository();
        var world = CreateWorld();
        world.ContentDatabase = content;
        var player = CreatePlayer();
        var relics = new RelicComponent();
        relics.RelicIds.Add("glass_cannon");
        player.SetComponent(relics);
        player.SetComponent(new ProgressionComponent { SelectedPerkIds = { "perk_berserker" } });
        var enemy = new StubEntity("Enemy", new Position(2, 1), Faction.Enemy);
        enemy.SetComponent(new EnemyComponent { TemplateId = "rat" });

        SynergyResolver.ApplyPassiveSynergies(player, content, world);
        var baseAttack = player.Stats.Attack;

        Expect.Equal(baseAttack + 6, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, baseAttack), "Authored damage synergy should add its flat bonus once.");
        Expect.Equal(baseAttack + 6, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, baseAttack), "Repeated outgoing hooks must not mutate or compound base attack.");
        Expect.Equal(baseAttack, player.Stats.Attack, "Damage synergies must not mutate base attack.");

        relics.RelicIds.Remove("glass_cannon");
        SynergyResolver.ApplyPassiveSynergies(player, content, world);
        Expect.Equal(baseAttack, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, baseAttack), "Deactivated damage synergies must stop contributing after reconciliation.");
    }

    private static void AuthoredHealTriggersOncePerEnemyKill()
    {
        var content = ContentLoader.LoadFromRepository();
        var world = CreateWorld();
        world.ContentDatabase = content;
        var player = CreatePlayer();
        player.Stats.HP = 5;
        var relics = new RelicComponent();
        relics.RelicIds.Add("vampire_fang");
        relics.RelicIds.Add("leech_stone");
        player.SetComponent(relics);
        world.AddEntity(player);
        var enemy = new StubEntity("Enemy", new Position(2, 1), Faction.Enemy);
        enemy.SetComponent(new EnemyComponent { TemplateId = "rat" });
        world.AddEntity(enemy);

        DeathResolver.ResolveKill(world, player, enemy);
        Expect.Equal(9, player.Stats.HP, "The authored heal synergy should restore its value once alongside the relic heal.");

        var duplicate = DeathResolver.ResolveKill(world, player, enemy);
        Expect.Equal(0, duplicate.KillsAwarded, "A removed enemy must not trigger the synergy again.");
        Expect.Equal(9, player.Stats.HP, "Duplicate kill resolution must not heal twice.");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(3, 3);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreatePlayer() =>
        new("Player", new Position(1, 1), Faction.Player, stats: new Stats { HP = 10, MaxHP = 10, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
}
