using System;
using System.IO;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class ComponentPersistenceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.Components preserves chest openability state", PreservesChestState);
        registry.Add("Persistence.Components preserves XP value", PreservesXpValue);
        registry.Add("Persistence.Components preserves enemy template identity", PreservesEnemyTemplateIdentity);
        registry.Add("Persistence.Components preserves trap component state", PreservesTrapComponentState);
        registry.Add("Persistence.Components preserves scheduler actor order", PreservesSchedulerActorOrder);
        registry.Add("Persistence.Components preserves abilities and cooldowns", PreservesAbilitiesAndCooldowns);
        registry.Add("Persistence.Components preserves AI state and brain", PreservesAIStateAndBrain);
        registry.Add("Persistence.Components preserves status source attribution", PreservesStatusSourceAttribution);
        registry.Add("Persistence.CombatResolver preserves RNG continuation", PreservesCombatRngContinuation);
        registry.Add("Persistence.CombatResolver preserves ability RNG continuation", PreservesAbilityRngContinuation);
        registry.Add("Persistence.Inventory preserves stack split id continuation", PreservesStackSplitIdContinuation);
    }

    private static void PreservesChestState()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var chest = CreateEntity("Chest", new Position(1, 0), Faction.Neutral, speed: 0, blocksMovement: false);
        var chestComponent = new ChestComponent { LootTableId = "vault_loot", HasRolled = true };
        var gem = new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true };
        chestComponent.Contents.Add(gem);
        chest.SetComponent(chestComponent);
        world.AddEntity(chest);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should allow non-actor chests with speed zero.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();

        Expect.NotNull(restored, "Saved world should load again.");
        var restoredChest = restored!.GetEntity(chest.Id);
        Expect.NotNull(restoredChest, "Chest entity should survive round-trip.");
        var restoredComponent = restoredChest!.GetComponent<ChestComponent>();
        Expect.Equal("vault_loot", restoredComponent?.LootTableId ?? string.Empty, "Chest loot table should survive round-trip.");
        Expect.True(restoredComponent?.HasRolled == true, "Rolled chest state should survive round-trip.");
        Expect.Equal(1, restoredComponent?.Contents.Count ?? 0, "Rolled chest contents should survive round-trip.");
        Expect.Equal(gem.InstanceId, restoredComponent!.Contents[0].InstanceId, "Chest item instance ids should survive round-trip.");
        Expect.Equal(2, restoredComponent.Contents[0].StackCount, "Chest item stack counts should survive round-trip.");
        Expect.Equal(0, restoredChest.Stats.Speed, "Chest speed zero should survive round-trip.");
    }

    private static void PreservesXpValue()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var enemy = CreateEntity("Rat", new Position(1, 0), Faction.Enemy);
        enemy.SetComponent(new XpValueComponent { Value = 17 });
        world.AddEntity(enemy);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with XP value component.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();

        Expect.NotNull(restored, "Saved world should load again.");
        Expect.Equal(17, restored!.GetEntity(enemy.Id)!.GetComponent<XpValueComponent>()?.Value ?? -1, "XP value should survive round-trip.");
    }

    private static void PreservesEnemyTemplateIdentity()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var enemy = CreateEntity("Goblin Archer", new Position(1, 0), Faction.Enemy);
        enemy.SetComponent(new EnemyComponent { TemplateId = "goblin_archer" });
        world.AddEntity(enemy);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with enemy component.");
        var restoredEnemy = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!.GetEntity(enemy.Id)!;
        var enemyComponent = restoredEnemy.GetComponent<EnemyComponent>();

        Expect.NotNull(enemyComponent, "Enemy component should survive round-trip.");
        Expect.Equal("goblin_archer", enemyComponent?.TemplateId ?? string.Empty, "Enemy template id should survive round-trip.");
    }

    private static void PreservesTrapComponentState()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        world.SetTile(new Position(2, 2), TileType.Trap);
        var trap = new Entity(
            "Trap",
            new Position(2, 2),
            new Stats { HP = 1, MaxHP = 1, Attack = 0, Accuracy = 0, Defense = 0, Evasion = 0, Speed = 0, ViewRadius = 0, Energy = 0 },
            Faction.Neutral,
            blocksMovement: false,
            blocksSight: false,
            id: EntityId.New());
        trap.SetComponent(new TrapComponent
        {
            TemplateId = "spike_trap",
            IsArmed = true,
            IsRevealed = false,
            TriggerCount = 0,
        });
        world.AddEntity(trap);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should persist trap component.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again.");
        var restoredTrap = restored!.GetEntity(trap.Id);
        Expect.NotNull(restoredTrap, "Trap entity should survive round-trip.");
        var restoredComponent = restoredTrap!.GetComponent<TrapComponent>();
        Expect.NotNull(restoredComponent, "Trap component should survive round-trip.");
        Expect.Equal("spike_trap", restoredComponent!.TemplateId, "Trap template id should survive round-trip.");
        Expect.True(restoredComponent.IsArmed, "Trap armed state should survive round-trip.");
        Expect.False(restoredComponent.IsRevealed, "Trap revealed state should survive round-trip.");
        Expect.Equal(0, restoredComponent.TriggerCount, "Trap trigger count should survive round-trip.");
    }

    private static void PreservesSchedulerActorOrder()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var first = CreateEntity("First", new Position(1, 0), Faction.Enemy, energy: 1200);
        var second = CreateEntity("Second", new Position(2, 0), Faction.Enemy, energy: 1200);
        world.AddEntity(first);
        world.AddEntity(second);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        scheduler.Register(first);
        scheduler.Register(second);
        scheduler.EndRound(world);
        scheduler.AttachWorld(world);
        var beforeSave = scheduler.GetNextActor();
        Expect.NotNull(beforeSave, "Scheduler should have a ready actor before save.");
        Expect.Equal(first.Id, beforeSave!.Id, "First registered actor should win the energy tie.");

        SyncSchedulerToWorld(world, scheduler);
        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with scheduler order.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again.");

        var restoredWorld = restored!;
        var restoredScheduler = new TurnScheduler();
        restoredScheduler.AttachWorld(restoredWorld);
        foreach (var entity in restoredWorld.Entities)
        {
            if (entity.Faction == Faction.Enemy)
            {
                restoredScheduler.Register(entity);
            }
        }

        restoredScheduler.NextOrder = restoredWorld.SchedulerNextOrder;
        var afterLoad = restoredScheduler.GetNextActor();
        Expect.NotNull(afterLoad, "Scheduler should have a ready actor after load.");
        Expect.Equal(first.Id, afterLoad!.Id, "Loaded scheduler should preserve energy tie-break order.");
    }

    private static void SyncSchedulerToWorld(WorldState world, TurnScheduler scheduler)
    {
        world.SchedulerOrders.Clear();
        foreach (var entity in world.Entities)
        {
            var order = scheduler.GetOrder(entity.Id);
            if (order != 0)
            {
                world.SchedulerOrders[entity.Id] = order;
            }
        }

        world.SchedulerNextOrder = scheduler.NextOrder;
    }

    private static void PreservesAbilitiesAndCooldowns()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var caster = CreateEntity("Caster", new Position(1, 0), Faction.Enemy);
        var abilities = new AbilitiesComponent();
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "firebolt", Cooldown = 3, Priority = 80 });
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "blink", Cooldown = 5, Priority = 20 });
        caster.SetComponent(abilities);
        var cooldowns = new CooldownComponent();
        cooldowns.SetCooldown("firebolt", 2);
        cooldowns.SetCooldown("expired", 0);
        caster.SetComponent(cooldowns);
        world.AddEntity(caster);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with abilities and cooldowns.");
        var restoredCaster = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!.GetEntity(caster.Id)!;
        var restoredAbilities = restoredCaster.GetComponent<AbilitiesComponent>();
        var restoredCooldowns = restoredCaster.GetComponent<CooldownComponent>();

        Expect.NotNull(restoredAbilities, "Abilities should survive round-trip.");
        Expect.Equal(2, restoredAbilities!.Slots.Count, "All ability slots should survive round-trip.");
        Expect.Equal("blink", restoredAbilities.Slots[1].AbilityId, "Ability slot order should survive round-trip.");
        Expect.Equal(20, restoredAbilities.Slots[1].Priority, "Ability priority should survive round-trip.");
        Expect.NotNull(restoredCooldowns, "Cooldown component should survive round-trip.");
        Expect.Equal(2, restoredCooldowns!.GetCooldown("firebolt"), "Active cooldown turns should survive round-trip.");
        Expect.Equal(0, restoredCooldowns.GetCooldown("expired"), "Expired cooldowns should remain inactive after round-trip.");
    }

    private static void PreservesAIStateAndBrain()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(5, 5);
        var target = CreateEntity("Target", new Position(2, 0), Faction.Player);
        world.AddEntity(target);
        var enemy = CreateEntity("Guard", new Position(1, 0), Faction.Enemy);
        enemy.SetComponent(new AIStateComponent
        {
            State = AIState.Patrol,
            IdleTurns = 2,
            PatrolTarget = new Position(3, 3),
            PatrolSteps = 4,
            PatrolSequence = 7,
            LastKnownTargetPosition = new Position(2, 0),
            TargetId = target.Id,
        });
        enemy.SetComponent<IBrain>(new PatrolGuardBrain());
        world.AddEntity(enemy);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with AI state and brain.");
        var restoredEnemy = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!.GetEntity(enemy.Id)!;
        var state = restoredEnemy.GetComponent<AIStateComponent>();

        Expect.NotNull(state, "AI state should survive round-trip.");
        Expect.Equal(AIState.Patrol, state!.State, "AI state enum should survive round-trip.");
        Expect.Equal(new Position(3, 3), state.PatrolTarget, "Patrol target should survive round-trip.");
        Expect.Equal(target.Id, state.TargetId, "Target id should survive round-trip.");
        Expect.True(restoredEnemy.GetComponent<IBrain>() is PatrolGuardBrain, "Saved brain type should be restored through BrainFactory.");
    }

    private static void PreservesStatusSourceAttribution()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var enemy = CreateEntity("Burning Rat", new Position(1, 0), Faction.Enemy);
        StatusEffectProcessor.ApplyEffect(enemy, StatusEffectType.Burning, 2, sourceEntityId: world.Player.Id);
        world.AddEntity(enemy);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed with sourced status effects.");
        var restoredEnemy = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!.GetEntity(enemy.Id)!;
        var restoredEffect = StatusEffectProcessor.GetEffect(restoredEnemy, StatusEffectType.Burning);

        Expect.NotNull(restoredEffect, "Status effect should survive round-trip.");
        Expect.Equal(world.Player.Id, restoredEffect!.SourceEntityId ?? EntityId.Invalid, "Status source id should survive round-trip for delayed kill attribution.");
    }

    private static void PreservesCombatRngContinuation()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var attacker = world.Player;
        var defender = CreateEntity("Dummy", new Position(1, 0), Faction.Enemy);
        world.AddEntity(defender);
        world.CombatResolver!.ResolveMeleeAttack(attacker, defender, world.TurnNumber);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed after combat RNG advances.");
        var expected = world.CombatResolver.ResolveMeleeAttack(attacker, defender, world.TurnNumber);
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        var actual = restored!.CombatResolver!.ResolveMeleeAttack(restored.Player, restored.GetEntity(defender.Id)!, restored.TurnNumber);

        Expect.Equal(expected.RawDamage, actual.RawDamage, "Saved combat RNG should continue with same raw damage.");
        Expect.Equal(expected.FinalDamage, actual.FinalDamage, "Saved combat RNG should continue with same final damage.");
        Expect.Equal(expected.IsCritical, actual.IsCritical, "Saved combat RNG should continue with same critical roll.");
        Expect.Equal(expected.IsMiss, actual.IsMiss, "Saved combat RNG should continue with same hit roll.");
    }

    private static void PreservesAbilityRngContinuation()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var attacker = world.Player;
        var defender = CreateEntity("Dummy", new Position(1, 0), Faction.Enemy);
        world.AddEntity(defender);
        var ability = new AbilityTemplate(
            "spark",
            "Spark",
            "Randomized test damage.",
            new AbilityTargeting("single", 8, 0, false, false, false, null),
            1000,
            null,
            new AbilityEffect[]
            {
                new("damage", DamageType.Lightning, 5, null, 0.0, null, 0, 0, "enemies", null, 0.0, null),
            });

        new CastAbilityAction(attacker.Id, ability, defender.Position).Execute(world);
        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed after ability RNG advances.");
        var expected = new CastAbilityAction(attacker.Id, ability, defender.Position).Execute(world).CombatEvents[0].DamageResults[0];
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!;
        var actual = new CastAbilityAction(restored.Player.Id, ability, restored.GetEntity(defender.Id)!.Position).Execute(restored).CombatEvents[0].DamageResults[0];

        Expect.Equal(expected.RawDamage, actual.RawDamage, "Saved ability RNG should continue with same raw damage.");
        Expect.Equal(expected.FinalDamage, actual.FinalDamage, "Saved ability RNG should continue with same final damage.");
    }

    private static void PreservesStackSplitIdContinuation()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        world.Player.SetComponent(new InventoryComponent());
        var inventory = world.Player.GetComponent<InventoryComponent>()!;
        var stack = new ItemInstance { TemplateId = "rock", StackCount = 3, IsIdentified = true };
        inventory.Add(stack);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed before a stack split.");
        var expectedOutcome = new DropItemAction(world.Player.Id, stack.InstanceId, 1).Execute(world);
        var expectedId = world.GetItemsAt(world.Player.Position)[0].InstanceId;

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult()!;
        var actualOutcome = new DropItemAction(restored.Player.Id, stack.InstanceId, 1).Execute(restored);
        var actualId = restored.GetItemsAt(restored.Player.Position)[0].InstanceId;

        Expect.Equal(ActionResult.Success, expectedOutcome.Result, "Uninterrupted split should succeed.");
        Expect.Equal(ActionResult.Success, actualOutcome.Result, "Loaded split should succeed.");
        Expect.Equal(expectedId, actualId, "Item id stream should continue identically after save/load.");
    }

    private static WorldState CreateWorld(int width, int height)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 1234;
        var player = CreateEntity("Hero", Position.Zero, Faction.Player);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

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

    private sealed class SaveSandbox : IDisposable
    {
        private SaveSandbox(string directoryPath, DateTime timestamp)
        {
            DirectoryPath = directoryPath;
            Timestamp = timestamp;
        }

        public string DirectoryPath { get; }

        private DateTime Timestamp { get; }

        public Func<DateTime> Clock => () => Timestamp;

        public static SaveSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-component-persistence-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new SaveSandbox(directoryPath, new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));
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
