using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class ActionTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Actions move action executes on open floor", MoveActionExecutesOnOpenFloor);
        registry.Add("Simulation.Actions move action rejects walls and blockers", MoveActionRejectsWallsAndBlockers);
        registry.Add("Simulation.Actions move action prevents corner cutting", MoveActionPreventsCornerCutting);
        registry.Add("Simulation.Actions attack action damages adjacent enemies", AttackActionDamagesAdjacentEnemies);
        registry.Add("Simulation.Actions attack action removes killed enemies", AttackActionRemovesKilledEnemies);
        registry.Add("Simulation.Actions attack action rejects same faction targets", AttackActionRejectsSameFaction);
        registry.Add("Simulation.Actions pickup action rejects full inventory", PickupActionRejectsFullInventory);
        registry.Add("Simulation.Actions pickup action merges stacks when bag is full", PickupActionMergesStacksWhenFull);
        registry.Add("Simulation.Actions use item heals and consumes potion", UseItemHealsAndConsumes);
        registry.Add("Simulation.Actions toggle equip action changes equipment state", ToggleEquipActionChangesEquipmentState);
        registry.Add("Simulation.Actions drop item places it on the ground", DropItemPlacesGroundItem);
        registry.Add("Simulation.Actions drop item can split a stack", DropItemSplitsStack);
        registry.Add("Simulation.Actions open and close door toggles walkability", OpenAndCloseDoorTogglesWalkability);
        registry.Add("Simulation.Actions stairs validation requires matching tile", StairsValidationRequiresMatchingTile);
    }

    private static void MoveActionExecutesOnOpenFloor()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);

        var action = new MoveAction(actor.Id, new Position(1, 0));
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Move action should succeed on open floor");
        Expect.Equal(new Position(2, 1), actor.Position, "Move action should update the actor position");
    }

    private static void MoveActionRejectsWallsAndBlockers()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var blocker = CreateActor("Blocker", new Position(2, 1), Faction.Enemy);

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(blocker);
        world.SetTile(new Position(1, 2), TileType.Wall);

        Expect.Equal(ActionResult.Blocked, new MoveAction(actor.Id, new Position(1, 0)).Validate(world), "Occupied destination should block movement");
        Expect.Equal(ActionResult.Blocked, new MoveAction(actor.Id, new Position(0, 1)).Validate(world), "Walls should block movement");
    }

    private static void MoveActionPreventsCornerCutting()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(2, 2), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(3, 2), TileType.Wall);
        world.SetTile(new Position(2, 3), TileType.Wall);

        var blockedDiagonal = new MoveAction(actor.Id, new Position(1, 1));
        Expect.Equal(ActionResult.Blocked, blockedDiagonal.Validate(world), "Diagonal movement should be blocked when both adjacent cardinals are blocked");

        world.SetTile(new Position(2, 3), TileType.Floor);
        Expect.Equal(ActionResult.Success, blockedDiagonal.Validate(world), "Diagonal movement should succeed when only one adjacent cardinal is blocked");
    }

    private static void AttackActionDamagesAdjacentEnemies()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 20, MaxHP = 20, Attack = 12, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var defender = CreateActor("Enemy", new Position(2, 1), Faction.Enemy, new Stats { HP = 12, MaxHP = 12, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        var outcome = new AttackAction(attacker.Id, defender.Id).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Attack action should execute successfully against adjacent enemies");
        Expect.True(defender.Stats.HP < 12, "Successful melee attacks should reduce the target HP");
        Expect.Equal(1, outcome.CombatEvents.Count, "Attack action should emit a combat event");
    }

    private static void AttackActionRemovesKilledEnemies()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 20, MaxHP = 20, Attack = 20, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var defender = CreateActor("Enemy", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 3, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        var outcome = new AttackAction(attacker.Id, defender.Id).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Killing attacks should still report success");
        Expect.True(world.GetEntity(defender.Id) is null, "Killed defender should be removed from the world");
    }

    private static void AttackActionRejectsSameFaction()
    {
        var world = CreateWorld();
        var left = CreateActor("AllyA", new Position(1, 1), Faction.Player);
        var right = CreateActor("AllyB", new Position(2, 1), Faction.Player);

        world.Player = left;
        world.AddEntity(left);
        world.AddEntity(right);

        var result = new AttackAction(left.Id, right.Id).Validate(world);
        Expect.Equal(ActionResult.Invalid, result, "Friendly targets should be rejected during validation");
    }

    private static void PickupActionRejectsFullInventory()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = new InventoryComponent(1);
        inventory.Add(new ItemInstance { TemplateId = "already_owned" });
        actor.SetComponent(inventory);

        world.Player = actor;
        world.AddEntity(actor);
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "potion_health" });

        Expect.Equal(ActionResult.Blocked, new PickupAction(actor.Id).Validate(world), "Pickup should fail when the inventory is full");
    }

    private static void PickupActionMergesStacksWhenFull()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = new InventoryComponent(1);
        inventory.Add(new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true });
        actor.SetComponent(inventory);

        world.Player = actor;
        world.AddEntity(actor);
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "potion_health", StackCount = 1, IsIdentified = true });

        var template = new ItemTemplate(
            "potion_health",
            "Health Potion",
            "Restores health.",
            ItemCategory.Consumable,
            EquipSlot.None,
            new Dictionary<string, int> { ["heal"] = 5 },
            "heal",
            -1,
            5);

        var action = new PickupAction(actor.Id, template);
        Expect.Equal(ActionResult.Success, action.Validate(world), "Pickup should validate when the new item can merge into an existing stack.");

        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Pickup should succeed when merging into an existing stack.");
        Expect.Equal(1, inventory.Items.Count, "Merging a pickup should avoid creating a second stack.");
        Expect.Equal(3, inventory.Items[0].StackCount, "The carried stack should absorb the picked-up item.");
        Expect.False(world.HasGroundItems(actor.Position), "Merged pickups should remove the source item from the ground.");
    }

    private static void UseItemHealsAndConsumes()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 4, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var inventory = new InventoryComponent();
        var item = new ItemInstance { TemplateId = "potion_health" };
        inventory.Add(item);
        actor.SetComponent(inventory);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "potion_health",
            "Health Potion",
            "Restores health.",
            ItemCategory.Consumable,
            EquipSlot.None,
            new Dictionary<string, int> { ["heal"] = 5 },
            "heal",
            -1,
            5);

        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Using a consumable should succeed when the item is in the inventory");
        Expect.Equal(9, actor.Stats.HP, "Health potion should heal the actor");
        Expect.False(inventory.Contains(item.InstanceId), "Consumed item should be removed from inventory");
    }

    private static void ToggleEquipActionChangesEquipmentState()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var sword = new ItemInstance { TemplateId = "sword_iron", IsIdentified = true };
        inventory.Add(sword);

        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate(
            "sword_iron",
            "Iron Sword",
            "Reliable steel.",
            ItemCategory.Weapon,
            EquipSlot.MainHand,
            new Dictionary<string, int> { ["attack"] = 2 },
            string.Empty,
            -1,
            1);

        var equip = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, equip.Result, "Equipping an owned item should succeed.");
        Expect.Equal(sword.InstanceId, inventory.GetEquipped(EquipSlot.MainHand)?.Item.InstanceId ?? EntityId.Invalid, "Equipping should place the item into the matching slot.");
        Expect.Equal(6, actor.Stats.Attack, "Equipping should apply the weapon stat bonus.");

        var unequip = new ToggleEquipAction(actor.Id, sword.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, unequip.Result, "Unequipping the same item should also succeed.");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is null, "Unequipping should clear the slot.");
        Expect.Equal(4, actor.Stats.Attack, "Unequipping should remove the weapon stat bonus.");
    }

    private static void DropItemPlacesGroundItem()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = new InventoryComponent();
        var item = new ItemInstance { TemplateId = "rock" };
        inventory.Add(item);
        actor.SetComponent(inventory);

        world.Player = actor;
        world.AddEntity(actor);

        var outcome = new DropItemAction(actor.Id, item.InstanceId).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Drop action should succeed for owned items");
        Expect.False(inventory.Contains(item.InstanceId), "Dropped item should be removed from inventory");
        Expect.True(world.HasGroundItems(actor.Position), "Dropped item should appear on the ground");
    }

    private static void DropItemSplitsStack()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var item = new ItemInstance { TemplateId = "rock", StackCount = 3, IsIdentified = true };
        inventory.Add(item);

        world.Player = actor;
        world.AddEntity(actor);

        var outcome = new DropItemAction(actor.Id, item.InstanceId, 1).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Split-drop should succeed for a stacked item.");
        Expect.Equal(2, inventory.Get(item.InstanceId)?.StackCount ?? 0, "Split-drop should leave the remaining stack in inventory.");
        Expect.Equal(1, world.GetItemsAt(actor.Position).Count, "Split-drop should create exactly one ground stack.");
        Expect.Equal(1, world.GetItemsAt(actor.Position)[0].StackCount, "The dropped stack should contain only the requested quantity.");
    }

    private static void OpenAndCloseDoorTogglesWalkability()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 1), TileType.Door);

        var open = new OpenDoorAction(actor.Id, new Position(2, 1));
        var openOutcome = open.Execute(world);

        Expect.Equal(ActionResult.Success, openOutcome.Result, "Opening a nearby closed door should succeed");
        Expect.True(world.IsDoorOpen(new Position(2, 1)), "Opening a door should mark the doorway as open");
        Expect.Equal(new Position(2, 1), actor.Position, "Opening a door by moving into it should place the actor in the doorway");

        world.MoveEntity(actor.Id, new Position(1, 1));
        Expect.True(world.IsWalkable(new Position(2, 1)), "Opened door should become walkable once the doorway is clear");

        var close = new CloseDoorAction(actor.Id, new Position(2, 1));
        var closeOutcome = close.Execute(world);
        Expect.Equal(ActionResult.Success, closeOutcome.Result, "Closing an open, empty doorway should succeed");
        Expect.False(world.IsWalkable(new Position(2, 1)), "Closed door should stop being walkable");
    }

    private static void StairsValidationRequiresMatchingTile()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 2), TileType.StairsDown);
        world.SetTile(new Position(3, 3), TileType.StairsUp);

        Expect.Equal(ActionResult.Invalid, new DescendAction(actor.Id).Validate(world), "Descending should fail away from down stairs");
        Expect.Equal(ActionResult.Invalid, new AscendAction(actor.Id).Validate(world), "Ascending should fail away from up stairs");

        world.MoveEntity(actor.Id, new Position(2, 2));
        Expect.Equal(ActionResult.Success, new DescendAction(actor.Id).Validate(world), "Descending should validate on down stairs");

        world.MoveEntity(actor.Id, new Position(3, 3));
        Expect.Equal(ActionResult.Success, new AscendAction(actor.Id).Validate(world), "Ascending should validate on up stairs");
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
}