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
        registry.Add("Persistence.Components preserves abilities and cooldowns", PreservesAbilitiesAndCooldowns);
        registry.Add("Persistence.Components preserves AI state and brain", PreservesAIStateAndBrain);
        registry.Add("Persistence.CombatResolver preserves RNG continuation", PreservesCombatRngContinuation);
    }

    private static void PreservesChestState()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(4, 4);
        var chest = CreateEntity("Chest", new Position(1, 0), Faction.Neutral, speed: 0, blocksMovement: false);
        chest.SetComponent(new ChestComponent { LootTableId = "vault_loot" });
        world.AddEntity(chest);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should allow non-actor chests with speed zero.");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();

        Expect.NotNull(restored, "Saved world should load again.");
        var restoredChest = restored!.GetEntity(chest.Id);
        Expect.NotNull(restoredChest, "Chest entity should survive round-trip.");
        Expect.Equal("vault_loot", restoredChest!.GetComponent<ChestComponent>()?.LootTableId ?? string.Empty, "Chest loot table should survive round-trip.");
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

    private static Entity CreateEntity(string name, Position position, Faction faction, int speed = 100, bool blocksMovement = true)
    {
        return new Entity(
            name,
            position,
            new Stats { HP = 20, MaxHP = 20, Attack = 10, Accuracy = 10, Defense = 1, Evasion = 0, Speed = speed, ViewRadius = 8, Energy = 1000 },
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
