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
        registry.Add("Simulation.Actions move action swaps neutral NPCs", MoveActionSwapsNeutralNpcs);
        registry.Add("Simulation.Actions move action rejects walls and blockers", MoveActionRejectsWallsAndBlockers);
        registry.Add("Simulation.Actions move action prevents corner cutting", MoveActionPreventsCornerCutting);
        registry.Add("Simulation.Actions attack action damages adjacent enemies", AttackActionDamagesAdjacentEnemies);
        registry.Add("Simulation.Actions attack action removes killed enemies", AttackActionRemovesKilledEnemies);
        registry.Add("Simulation.Actions attack action rejects same faction targets", AttackActionRejectsSameFaction);
        registry.Add("Simulation.Actions attack action rejects neutral chest targets", AttackActionRejectsNeutralChestTargets);
        registry.Add("Simulation.Actions pickup action rejects full inventory", PickupActionRejectsFullInventory);
        registry.Add("Simulation.Actions pickup action merges stacks when bag is full", PickupActionMergesStacksWhenFull);
        registry.Add("Simulation.Actions pickup action resolves stack template from content", PickupActionResolvesStackTemplateFromContent);
        registry.Add("Simulation.Actions pickup auto-equips strict upgrades only when enabled", PickupAutoEquipsStrictUpgradesOnlyWhenEnabled);
        registry.Add("Simulation.Actions pickup auto-equip respects requirements", PickupAutoEquipRespectsRequirements);
        registry.Add("Simulation.Actions use item heals and consumes potion", UseItemHealsAndConsumes);
        registry.Add("Simulation.Actions use item consumes one stacked potion", UseItemConsumesOneStackedPotion);
        registry.Add("Simulation.Actions toggle equip action changes equipment state", ToggleEquipActionChangesEquipmentState);
        registry.Add("Simulation.Actions drop item places it on the ground", DropItemPlacesGroundItem);
        registry.Add("Simulation.Actions drop item can split a stack", DropItemSplitsStack);
        registry.Add("Simulation.Actions stack split ids are deterministic", StackSplitIdsAreDeterministic);
        registry.Add("Simulation.Actions open and close door toggles walkability", OpenAndCloseDoorTogglesWalkability);
        registry.Add("Simulation.Actions open door moves through when exit tile is clear", OpenDoorMovesThroughWhenExitTileIsClear);
        registry.Add("Simulation.Actions open locked door consumes key", OpenLockedDoorConsumesKey);
        registry.Add("Simulation.Actions open locked door fails without key", OpenLockedDoorFailsWithoutKey);
        registry.Add("Simulation.Actions open chest drops loot and removes chest", OpenChestDropsLootAndRemovesChest);
        registry.Add("Simulation.Actions open chest reports loot names in the log", OpenChestReportsLootNamesInLog);
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

    private static void MoveActionSwapsNeutralNpcs()
    {
        var world = CreateWorld();
        var player = CreateActor("Player", new Position(1, 1), Faction.Player);
        var npc = new Entity("Guide", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 }, Faction.Neutral);
        npc.SetComponent(new NpcComponent { TemplateId = "guide", Role = "advisor", DialogueId = "guide_intro" });

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(npc);

        var outcome = new MoveAction(player.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Moving into a neutral NPC should execute the validated swap.");
        Expect.Equal(new Position(2, 1), player.Position, "Player should move into the NPC's previous tile.");
        Expect.Equal(new Position(1, 1), npc.Position, "NPC should move into the player's previous tile.");
        Expect.Equal(player.Id, world.GetEntityAt(new Position(2, 1))?.Id ?? EntityId.Invalid, "Position index should track the swapped player.");
        Expect.Equal(npc.Id, world.GetEntityAt(new Position(1, 1))?.Id ?? EntityId.Invalid, "Position index should track the swapped NPC.");
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

    private static void AttackActionRejectsNeutralChestTargets()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Enemy", new Position(1, 1), Faction.Enemy);
        var chest = new Entity("Treasure Chest", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0 }, Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "chest_loot" });

        world.AddEntity(attacker);
        world.AddEntity(chest);

        var action = new AttackAction(attacker.Id, chest.Id);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Invalid, action.Validate(world), "Neutral chests should not validate as melee targets.");
        Expect.Equal(ActionResult.Invalid, outcome.Result, "Executing an attack against a neutral chest should fail.");
        Expect.Equal(1, chest.Stats.HP, "Invalid attacks must not damage the chest.");
        Expect.True(world.GetEntity(chest.Id) is not null, "Invalid attacks must not remove the chest.");
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
            5,
            "common");

        var action = new PickupAction(actor.Id, template);
        Expect.Equal(ActionResult.Success, action.Validate(world), "Pickup should validate when the new item can merge into an existing stack.");

        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Pickup should succeed when merging into an existing stack.");
        Expect.Equal(1, inventory.Items.Count, "Merging a pickup should avoid creating a second stack.");
        Expect.Equal(3, inventory.Items[0].StackCount, "The carried stack should absorb the picked-up item.");
        Expect.False(world.HasGroundItems(actor.Position), "Merged pickups should remove the source item from the ground.");
    }

    private static void PickupActionResolvesStackTemplateFromContent()
    {
        var world = CreateWorld();
        world.ContentDatabase = new StubContentDatabase();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = new InventoryComponent(1);
        inventory.Add(new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true });
        actor.SetComponent(inventory);

        world.Player = actor;
        world.AddEntity(actor);
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "potion_health", StackCount = 1, IsIdentified = true });

        var action = new PickupAction(actor.Id);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Pickup should resolve stack metadata from world content without a caller-supplied template.");
        Expect.Equal(1, inventory.Items.Count, "Resolved stack pickups should avoid creating a second stack.");
        Expect.Equal(3, inventory.Items[0].StackCount, "Resolved stack pickups should merge into the carried stack.");
        Expect.False(world.HasGroundItems(actor.Position), "Merged content-resolved pickups should remove the source ground item.");
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
            5,
            "common");

        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Using a consumable should succeed when the item is in the inventory");
        Expect.Equal(9, actor.Stats.HP, "Health potion should heal the actor");
        Expect.False(inventory.Contains(item.InstanceId), "Consumed item should be removed from inventory");
    }

    private static void UseItemConsumesOneStackedPotion()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 4, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var inventory = new InventoryComponent();
        var item = new ItemInstance { TemplateId = "potion_health", StackCount = 3 };
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
            5,
            "common");

        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Using a stacked consumable should succeed.");
        Expect.True(inventory.Contains(item.InstanceId), "Using one item from a stack should keep the remaining stack.");
        Expect.Equal(2, inventory.Get(item.InstanceId)?.StackCount ?? 0, "Using a stacked consumable should consume exactly one item.");
        Expect.Equal(9, actor.Stats.HP, "The consumed potion should still apply its effect once.");
    }

    private static void PickupAutoEquipsStrictUpgradesOnlyWhenEnabled()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var oldSword = new ItemInstance { TemplateId = "old_sword", IsIdentified = true };
        inventory.Add(oldSword);
        inventory.TryEquip(oldSword, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 1 }, out _);
        actor.Stats.Attack += 1;
        world.Player = actor;
        world.AddEntity(actor);

        var betterSwordTemplate = new ItemTemplate("better_sword", "Better Sword", "Sharper.", ItemCategory.Weapon, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 2 }, string.Empty, -1, 1, "common");
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "better_sword", IsIdentified = true });

        var disabled = new PickupAction(actor.Id, betterSwordTemplate).Execute(world);
        Expect.Equal(ActionResult.Success, disabled.Result, "Pickup should still succeed when auto-equip is off.");
        Expect.Equal(oldSword.InstanceId, inventory.GetEquipped(EquipSlot.MainHand)?.Item.InstanceId ?? EntityId.Invalid, "Auto-equip defaults off and should not replace equipment.");
        Expect.Equal(5, actor.Stats.Attack, "Stats should not change when auto-equip is disabled.");

        var autoSwordTemplate = new ItemTemplate("auto_sword", "Auto Sword", "Sharper still.", ItemCategory.Weapon, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 2 }, string.Empty, -1, 1, "common");
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "auto_sword", IsIdentified = true });
        var autoEquipped = new PickupAction(actor.Id, autoSwordTemplate, autoEquipUpgrades: true).Execute(world);
        var autoSword = inventory.GetEquipped(EquipSlot.MainHand)?.Item;
        Expect.Equal(ActionResult.Success, autoEquipped.Result, "Strict upgrade pickup should succeed.");
        Expect.Equal("auto_sword", autoSword?.TemplateId ?? string.Empty, "Strict upgrades should auto-equip when enabled.");
        Expect.Equal(6, actor.Stats.Attack, "Auto-equip should replace old modifiers with new modifiers.");
        Expect.True(autoEquipped.LogMessages.Exists(message => message.Contains("auto-equips Auto Sword")), "Auto-equip should be logged explicitly.");

        var equalTemplate = new ItemTemplate("equal_sword", "Equal Sword", "Same.", ItemCategory.Weapon, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 2 }, string.Empty, -1, 1, "common");
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "equal_sword", IsIdentified = true });
        var equal = new PickupAction(actor.Id, equalTemplate, autoEquipUpgrades: true).Execute(world);
        Expect.Equal(ActionResult.Success, equal.Result, "Equal pickup should succeed.");
        Expect.Equal(autoSword?.InstanceId ?? EntityId.Invalid, inventory.GetEquipped(EquipSlot.MainHand)?.Item.InstanceId ?? EntityId.Invalid, "Equal equipment must not be auto-equipped.");
        Expect.Equal(6, actor.Stats.Attack, "Equal equipment should not alter stats.");

        var stackableTemplate = new ItemTemplate("stackable_blade", "Stackable Blade", "Invalid stackable equipment.", ItemCategory.Weapon, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 9 }, string.Empty, -1, 3, "common");
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "stackable_blade", IsIdentified = true });
        new PickupAction(actor.Id, stackableTemplate, autoEquipUpgrades: true).Execute(world);
        Expect.Equal(autoSword?.InstanceId ?? EntityId.Invalid, inventory.GetEquipped(EquipSlot.MainHand)?.Item.InstanceId ?? EntityId.Invalid, "Stackable items must not be auto-equipped.");

        var betterItem = inventory.Items[1];
        var equip = new ToggleEquipAction(actor.Id, betterItem.InstanceId, betterSwordTemplate).Execute(world);
        Expect.Equal(ActionResult.Success, equip.Result, "Manual equip should still work for the previously picked upgrade.");
        Expect.Equal(6, actor.Stats.Attack, "Manual equip and auto-equip use the same stat modifier path.");
    }

    private static void PickupAutoEquipRespectsRequirements()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var inventory = actor.GetComponent<InventoryComponent>()!;
        world.Player = actor;
        world.AddEntity(actor);

        var template = new ItemTemplate("heavy_axe", "Heavy Axe", "Too heavy.", ItemCategory.Weapon, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 5 }, string.Empty, -1, 1, "common", Requirements: new Dictionary<string, int> { ["attack"] = 99 });
        world.DropItem(actor.Position, new ItemInstance { TemplateId = "heavy_axe", IsIdentified = true });

        var outcome = new PickupAction(actor.Id, template, autoEquipUpgrades: true).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Requirement-blocked equipment should still be picked up.");
        Expect.True(inventory.GetEquipped(EquipSlot.MainHand) is null, "Requirement-blocked equipment must not auto-equip.");
        Expect.Equal(4, actor.Stats.Attack, "Blocked auto-equip should not apply modifiers.");
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
            1,
            "common");

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

    private static void StackSplitIdsAreDeterministic()
    {
        var first = SplitStackInNewWorld();
        var second = SplitStackInNewWorld();

        Expect.Equal(first, second, "Splitting the same stack in same-seeded worlds should allocate the same dropped stack id.");
    }

    private static EntityId SplitStackInNewWorld()
    {
        var world = CreateWorld(seed: 777);
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var item = new ItemInstance { TemplateId = "rock", StackCount = 3, IsIdentified = true };
        inventory.Add(item);
        world.Player = actor;
        world.AddEntity(actor);

        new DropItemAction(actor.Id, item.InstanceId, 1).Execute(world);
        return world.GetItemsAt(actor.Position)[0].InstanceId;
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
        Expect.Equal(new Position(3, 1), actor.Position, "Opening a clear doorway should move the actor through it");

        world.MoveEntity(actor.Id, new Position(1, 1));
        Expect.True(world.IsWalkable(new Position(2, 1)), "Opened door should become walkable once the doorway is clear");

        var close = new CloseDoorAction(actor.Id, new Position(2, 1));
        var closeOutcome = close.Execute(world);
        Expect.Equal(ActionResult.Success, closeOutcome.Result, "Closing an open, empty doorway should succeed");
        Expect.False(world.IsWalkable(new Position(2, 1)), "Closed door should stop being walkable");
    }

    private static void OpenDoorMovesThroughWhenExitTileIsClear()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 1), TileType.Door);

        var outcome = new OpenDoorAction(actor.Id, new Position(2, 1)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Opening a clear door should succeed");
        Expect.Equal(new Position(3, 1), actor.Position, "Actor should move to the tile beyond the opened door when it is available");
        Expect.True(world.IsDoorOpen(new Position(2, 1)), "Door should remain open after traversal");
    }

    private static void OpenLockedDoorConsumesKey()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var inventory = actor.GetComponent<InventoryComponent>()!;
        inventory.Add(new ItemInstance { TemplateId = "dungeon_key", StackCount = 1, IsIdentified = true });

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 1), TileType.LockedDoor);

        var outcome = new OpenDoorAction(actor.Id, new Position(2, 1)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Opening a locked door with a key should succeed");
        Expect.Equal(TileType.Door, world.GetTile(new Position(2, 1)), "Locked door should become a regular door after unlocking");
        Expect.True(world.IsDoorOpen(new Position(2, 1)), "Unlocked door should be open");
        Expect.Equal(0, inventory.Items.Count, "Key should be consumed when unlocking the door");
        Expect.Equal(new Position(3, 1), actor.Position, "Actor should move through the unlocked door");
    }

    private static void OpenLockedDoorFailsWithoutKey()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);

        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 1), TileType.LockedDoor);

        var outcome = new OpenDoorAction(actor.Id, new Position(2, 1)).Execute(world);

        Expect.Equal(ActionResult.Blocked, outcome.Result, "Opening a locked door without a key should be blocked");
        Expect.Equal(TileType.LockedDoor, world.GetTile(new Position(2, 1)), "Locked door should remain locked when the actor has no key");
        Expect.Equal(new Position(1, 1), actor.Position, "Actor should not move when unlocking fails");
    }

    private static void OpenChestDropsLootAndRemovesChest()
    {
        var world = CreateWorld(seed: 42);
        world.Depth = 2;
        world.ContentDatabase = ContentLoader.LoadFromDirectory(ContentLoader.FindContentDirectory());

        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var chest = new Entity("Treasure Chest", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0 }, Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "chest_loot" });

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(chest);

        var outcome = new OpenChestAction(actor.Id, chest.Id).Execute(world);

        var inventory = actor.GetComponent<InventoryComponent>()!;

        Expect.Equal(ActionResult.Success, outcome.Result, "Opening an adjacent chest should succeed.");
        Expect.True(world.GetEntity(chest.Id) is null, "Opened chests should be removed from the world.");
        Expect.True(inventory.Items.Count > 0, "Opening a chest with room in the pack should move loot directly into the actor inventory.");
        Expect.False(world.HasGroundItems(new Position(2, 1)), "Opening a chest with room in the pack should not leave the loot on the floor.");
    }

    private static void OpenChestReportsLootNamesInLog()
    {
        var world = CreateWorld(seed: 0);
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var chest = new Entity("Treasure Chest", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0 }, Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "chest_loot" });
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        world.ContentDatabase = content;

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(chest);

        var outcome = new OpenChestAction(actor.Id, chest.Id).Execute(world);

        Expect.True(outcome.LogMessages.Count > 0, "Opening a chest should emit a log message.");
        Expect.True(outcome.LogMessages[0].Contains("Loot found:", System.StringComparison.Ordinal), "Chest logs should explicitly call out the found loot.");
        Expect.True(outcome.LogMessages[0].Contains("Stowed:", System.StringComparison.Ordinal) || outcome.LogMessages[0].Contains("Spilled onto the floor:", System.StringComparison.Ordinal), "Chest logs should explain where the loot went instead of only removing the chest.");
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
