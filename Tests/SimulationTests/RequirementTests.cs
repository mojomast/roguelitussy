using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class RequirementTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Requirements blocks equip below level", BlocksEquipBelowLevel);
        registry.Add("Simulation.Requirements allows equip at level", AllowsEquipAtLevel);
        registry.Add("Simulation.Requirements unequip always succeeds", UnequipAlwaysSucceeds);
        registry.Add("Simulation.Requirements no requirements always passes", NoRequirementsAlwaysPasses);
        registry.Add("Simulation.Requirements reports failed requirement details", ReportsFailedRequirementDetails);
    }

    private static void BlocksEquipBelowLevel()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        actor.SetComponent(new ProgressionComponent { Level = 1 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var sword = new ItemInstance { TemplateId = "sword_high", IsIdentified = true };
        inventory.Add(sword);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "sword_high",
            "High-Level Sword",
            "Too powerful for beginners.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 5 },
            null,
            0,
            1,
            "rare",
            Requirements: new Dictionary<string, int> { ["level"] = 5 });

        var outcome = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Blocked, outcome.Result, "Equipping should be blocked when level requirement is not met");
        Expect.True(outcome.LogMessages.Count > 0, "Should include a failure message");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is null, "Item should not be equipped");
    }

    private static void AllowsEquipAtLevel()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        actor.SetComponent(new ProgressionComponent { Level = 5 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var sword = new ItemInstance { TemplateId = "sword_high", IsIdentified = true };
        inventory.Add(sword);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "sword_high",
            "High-Level Sword",
            "Power at the right level.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 5 },
            null,
            0,
            1,
            "rare",
            Requirements: new Dictionary<string, int> { ["level"] = 5 });

        var outcome = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Equipping should succeed when level requirement is met");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is not null, "Item should be equipped");
    }

    private static void UnequipAlwaysSucceeds()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        actor.SetComponent(new ProgressionComponent { Level = 5 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var sword = new ItemInstance { TemplateId = "sword_high", IsIdentified = true };
        inventory.Add(sword);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "sword_high",
            "High-Level Sword",
            "Already equipped.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 5 },
            null,
            0,
            1,
            "rare",
            Requirements: new Dictionary<string, int> { ["level"] = 5 });

        // Equip first
        new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is not null, "Item should be equipped first");

        // Now unequip - should always succeed regardless of requirements
        var unequipOutcome = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, unequipOutcome.Result, "Unequipping should always succeed");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is null, "Item should be unequipped");
    }

    private static void NoRequirementsAlwaysPasses()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        // No ProgressionComponent set - should default to level 1
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var sword = new ItemInstance { TemplateId = "sword_basic", IsIdentified = true };
        inventory.Add(sword);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "sword_basic",
            "Basic Sword",
            "No requirements.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 2 },
            null,
            0,
            1,
            "common");

        var outcome = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Equipping with no requirements should always succeed");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is not null, "Item should be equipped");
    }

    private static void ReportsFailedRequirementDetails()
    {
        var actor = new StubEntity("Player", new Position(0, 0), Faction.Player,
            stats: new Stats { HP = 10, MaxHP = 10, Attack = 2, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        actor.SetComponent(new ProgressionComponent { Level = 3 });

        var template = new ItemTemplate(
            "sword_elite",
            "Elite Sword",
            "Demands greatness.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 8 },
            null,
            0,
            1,
            "legendary",
            Requirements: new Dictionary<string, int> { ["level"] = 10, ["strength"] = 5 });

        Expect.False(RequirementValidator.MeetsRequirements(actor, template), "Should not meet requirements");

        var failures = RequirementValidator.GetFailedRequirements(actor, template);
        Expect.Equal(2, failures.Count, "Should report two failed requirements");
        Expect.True(failures.Exists(f => f.Contains("level")), "Should report level failure");
        Expect.True(failures.Exists(f => f.Contains("strength")), "Should report strength failure");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = 123;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction)
    {
        var actor = new StubEntity(name, position, faction,
            stats: new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        actor.SetComponent(new InventoryComponent());
        return actor;
    }
}
