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

    private static StubEntity CreatePlayer() =>
        new("Player", new Position(1, 1), Faction.Player, stats: new Stats { HP = 10, MaxHP = 10, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
}
