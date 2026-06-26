using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class DeterminismTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Determinism.Replay survives save/load at turn 3", ReplaySurvivesSaveLoadAtTurn3);
        registry.Add("Determinism.Replay survives save/load at turn 7", ReplaySurvivesSaveLoadAtTurn7);
        registry.Add("Determinism.RNG rehydration has no transient calls", RngRehydrationHasNoTransientCalls);
    }

    private static void ReplaySurvivesSaveLoadAtTurn3()
    {
        var expected = RunUninterrupted(7);
        var actual = RunWithSaveLoad(new[] { 3 });
        CompareTraces(expected, actual);
    }

    private static void ReplaySurvivesSaveLoadAtTurn7()
    {
        var expected = RunUninterrupted(7);
        var actual = RunWithSaveLoad(new[] { 7 });
        CompareTraces(expected, actual);
    }

    private static void RngRehydrationHasNoTransientCalls()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateSeededWorld(42);

        var player = world.Player;
        var enemy = world.Entities.First(entity => entity.Id != player.Id);
        new AttackAction(player.Id, enemy.Id).Execute(world);

        var combatRng = new DeterministicRandom(world.CombatRandomState);
        var itemRng = new DeterministicRandom(world.ItemRandomState);
        var combatPeeks = new List<ulong>();
        var itemPeeks = new List<ulong>();
        for (var i = 0; i < 20; i++)
        {
            combatPeeks.Add(combatRng.Peek());
            itemPeeks.Add(itemRng.Peek());
        }

        SyncSchedulerStateToWorld(world, new TurnScheduler());
        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed for RNG rehydration test");
        var loaded = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(loaded, "Loaded world should not be null");

        var loadedCombatRng = new DeterministicRandom(loaded!.CombatRandomState);
        var loadedItemRng = new DeterministicRandom(loaded.ItemRandomState);
        for (var i = 0; i < 20; i++)
        {
            Expect.Equal(combatPeeks[i], loadedCombatRng.Peek(), $"Combat RNG peek {i} should match after load");
            Expect.Equal(itemPeeks[i], loadedItemRng.Peek(), $"Item RNG peek {i} should match after load");
        }

        var originalConsumedCombat = new DeterministicRandom(world.CombatRandomState).Next(100);
        var loadedConsumedCombat = new DeterministicRandom(loaded.CombatRandomState).Next(100);
        Expect.Equal(originalConsumedCombat, loadedConsumedCombat, "Next combat random output after load should match uninterrupted");

        var originalConsumedItem = new DeterministicRandom(world.ItemRandomState).Next(100);
        var loadedConsumedItem = new DeterministicRandom(loaded.ItemRandomState).Next(100);
        Expect.Equal(originalConsumedItem, loadedConsumedItem, "Next item random output after load should match uninterrupted");
    }

    private static IReadOnlyList<TraceFrame> RunUninterrupted(int actionCount)
    {
        var world = CreateSeededWorld(12345);
        var scheduler = new OneActorScheduler();
        var loop = new GameLoop();
        var actions = BuildActionSequence(world);

        var trace = new List<TraceFrame>();
        for (var i = 0; i < actionCount; i++)
        {
            var outcome = loop.ProcessRound(world, scheduler, _ => actions[i]);
            trace.Add(CaptureFrame(world, outcome));
        }

        return trace;
    }

    private static IReadOnlyList<TraceFrame> RunWithSaveLoad(IReadOnlyList<int> saveAfterTurns)
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateSeededWorld(12345);
        var scheduler = new OneActorScheduler();
        var loop = new GameLoop();
        var actions = BuildActionSequence(world);

        var trace = new List<TraceFrame>();
        for (var i = 0; i < actions.Count; i++)
        {
            var outcome = loop.ProcessRound(world, scheduler, _ => actions[i]);
            trace.Add(CaptureFrame(world, outcome));

            if (saveAfterTurns.Contains(i + 1))
            {
                SyncSchedulerStateToWorld(world, scheduler);
                Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), $"Save should succeed at turn {i + 1}");
                var loaded = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
                Expect.NotNull(loaded, $"Load should succeed at turn {i + 1}");
                world = loaded!;
                scheduler = new OneActorScheduler();
                scheduler.NextOrder = world.SchedulerNextOrder;
            }
        }

        return trace;
    }

    private static WorldState CreateSeededWorld(int seed)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = seed;
        world.Depth = 0;
        world.TurnNumber = 0;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity(
            "Hero",
            new Position(1, 1),
            new Stats
            {
                HP = 40,
                MaxHP = 40,
                Attack = 10,
                Defense = 2,
                Accuracy = 5,
                Evasion = 5,
                Speed = 100,
                ViewRadius = 8,
                Energy = 1000,
            },
            Faction.Player,
            id: new EntityId(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        var enemy = new Entity(
            "Goblin",
            new Position(4, 1),
            new Stats
            {
                HP = 20,
                MaxHP = 20,
                Attack = 3,
                Defense = 0,
                Accuracy = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
                Energy = -1_000_000,
            },
            Faction.Enemy,
            id: new EntityId(Guid.Parse("22222222-2222-2222-2222-222222222222")));

        player.SetComponent(new InventoryComponent());
        player.SetComponent(new ProgressionComponent());
        player.SetComponent(new WalletComponent { Gold = 0 });
        enemy.SetComponent(new XpValueComponent { Value = 10 });

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        return world;
    }

    private static IReadOnlyList<IAction> BuildActionSequence(WorldState world)
    {
        var player = world.Player;
        var enemy = world.Entities.First(entity => entity.Id != player.Id);
        return new IAction[]
        {
            new MoveAction(player.Id, new Position(1, 0)),
            new MoveAction(player.Id, new Position(1, 0)),
            new AttackAction(player.Id, enemy.Id),
            new WaitAction(player.Id),
            new AttackAction(player.Id, enemy.Id),
            new WaitAction(player.Id),
            new AttackAction(player.Id, enemy.Id),
        };
    }

    private static TraceFrame CaptureFrame(WorldState world, ActionOutcome outcome)
    {
        var player = world.Player;
        var enemy = world.Entities.FirstOrDefault(entity => entity.Id != player.Id);
        return new TraceFrame(
            world.TurnNumber,
            player.Position,
            enemy?.Position ?? Position.Invalid,
            player.Stats.HP,
            enemy?.Stats.HP ?? 0,
            world.CombatRandomState,
            world.ItemRandomState,
            outcome.LogMessages.ToList(),
            outcome.CombatEvents.Select(CreateCombatEventSnapshot).ToList());
    }

    private static CombatEventSnapshot CreateCombatEventSnapshot(CombatEvent combatEvent)
    {
        return new CombatEventSnapshot(
            combatEvent.TurnNumber,
            combatEvent.ActionType,
            combatEvent.DamageResults.Select(damage => new DamageResultSnapshot(
                damage.AttackerId,
                damage.DefenderId,
                damage.RawDamage,
                damage.FinalDamage,
                damage.IsMiss,
                damage.IsKill)).ToList(),
            combatEvent.StatusEffectsApplied.Select(effect => effect.Type.ToString()).ToList());
    }

    private static void CompareTraces(IReadOnlyList<TraceFrame> expected, IReadOnlyList<TraceFrame> actual)
    {
        Expect.Equal(expected.Count, actual.Count, "Trace frame counts should match");
        for (var i = 0; i < expected.Count; i++)
        {
            var expectedFrame = expected[i];
            var actualFrame = actual[i];
            var prefix = $"Frame {i + 1}";

            Expect.Equal(expectedFrame.TurnNumber, actualFrame.TurnNumber, $"{prefix}: turn number should match");
            Expect.Equal(expectedFrame.PlayerPosition, actualFrame.PlayerPosition, $"{prefix}: player position should match");
            Expect.Equal(expectedFrame.EnemyPosition, actualFrame.EnemyPosition, $"{prefix}: enemy position should match");
            Expect.Equal(expectedFrame.PlayerHp, actualFrame.PlayerHp, $"{prefix}: player HP should match");
            Expect.Equal(expectedFrame.EnemyHp, actualFrame.EnemyHp, $"{prefix}: enemy HP should match");
            Expect.Equal(expectedFrame.CombatRandomState, actualFrame.CombatRandomState, $"{prefix}: combat random state should match");
            Expect.Equal(expectedFrame.ItemRandomState, actualFrame.ItemRandomState, $"{prefix}: item random state should match");

            CompareLogMessages(expectedFrame.LogMessages, actualFrame.LogMessages, prefix);
            CompareCombatEvents(expectedFrame.CombatEvents, actualFrame.CombatEvents, prefix);
        }
    }

    private static void CompareLogMessages(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string prefix)
    {
        Expect.Equal(expected.Count, actual.Count, $"{prefix}: log message count should match");
        for (var i = 0; i < expected.Count; i++)
        {
            Expect.Equal(expected[i], actual[i], $"{prefix}: log message {i + 1} should match");
        }
    }

    private static void CompareCombatEvents(IReadOnlyList<CombatEventSnapshot> expected, IReadOnlyList<CombatEventSnapshot> actual, string prefix)
    {
        Expect.Equal(expected.Count, actual.Count, $"{prefix}: combat event count should match");
        for (var i = 0; i < expected.Count; i++)
        {
            var expectedEvent = expected[i];
            var actualEvent = actual[i];
            var eventPrefix = $"{prefix}: combat event {i + 1}";

            Expect.Equal(expectedEvent.TurnNumber, actualEvent.TurnNumber, $"{eventPrefix}: turn number should match");
            Expect.Equal(expectedEvent.ActionType, actualEvent.ActionType, $"{eventPrefix}: action type should match");

            Expect.Equal(expectedEvent.DamageResults.Count, actualEvent.DamageResults.Count, $"{eventPrefix}: damage result count should match");
            for (var d = 0; d < expectedEvent.DamageResults.Count; d++)
            {
                var expectedDamage = expectedEvent.DamageResults[d];
                var actualDamage = actualEvent.DamageResults[d];
                var damagePrefix = $"{eventPrefix}: damage result {d + 1}";

                Expect.Equal(expectedDamage.AttackerId, actualDamage.AttackerId, $"{damagePrefix}: attacker ID should match");
                Expect.Equal(expectedDamage.DefenderId, actualDamage.DefenderId, $"{damagePrefix}: defender ID should match");
                Expect.Equal(expectedDamage.RawDamage, actualDamage.RawDamage, $"{damagePrefix}: raw damage should match");
                Expect.Equal(expectedDamage.FinalDamage, actualDamage.FinalDamage, $"{damagePrefix}: final damage should match");
                Expect.Equal(expectedDamage.IsMiss, actualDamage.IsMiss, $"{damagePrefix}: miss flag should match");
                Expect.Equal(expectedDamage.IsKill, actualDamage.IsKill, $"{damagePrefix}: kill flag should match");
            }

            CompareLogMessages(expectedEvent.StatusEffectsApplied, actualEvent.StatusEffectsApplied, eventPrefix + " status effects");
        }
    }

    private static void SyncSchedulerStateToWorld(WorldState world, ITurnScheduler scheduler)
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

    private sealed record TraceFrame(
        int TurnNumber,
        Position PlayerPosition,
        Position EnemyPosition,
        int PlayerHp,
        int EnemyHp,
        ulong CombatRandomState,
        ulong ItemRandomState,
        IReadOnlyList<string> LogMessages,
        IReadOnlyList<CombatEventSnapshot> CombatEvents);

    private sealed record CombatEventSnapshot(
        int TurnNumber,
        ActionType ActionType,
        IReadOnlyList<DamageResultSnapshot> DamageResults,
        IReadOnlyList<string> StatusEffectsApplied);

    private sealed record DamageResultSnapshot(
        EntityId AttackerId,
        EntityId DefenderId,
        int RawDamage,
        int FinalDamage,
        bool IsMiss,
        bool IsKill);

    private sealed class OneActorScheduler : ITurnScheduler
    {
        private readonly TurnScheduler _inner = new();
        private IEntity? _nextActor;
        private bool _consumed;

        public int EnergyThreshold => _inner.EnergyThreshold;

        public void BeginRound(WorldState world)
        {
            _consumed = false;
            _inner.BeginRound(world);
            _nextActor = _inner.GetNextActor();
        }

        public bool HasNextActor() => !_consumed && _nextActor is { IsAlive: true };

        public IEntity? GetNextActor() => _consumed ? null : _nextActor;

        public StatusTickResult? ConsumeEnergy(EntityId actorId, int cost)
        {
            _consumed = true;
            return _inner.ConsumeEnergy(actorId, cost);
        }

        public void EndRound(WorldState world)
        {
            _inner.EndRound(world);
            _nextActor = null;
        }

        public void Register(IEntity entity) => _inner.Register(entity);

        public void Unregister(EntityId id) => _inner.Unregister(id);

        public int GetOrder(EntityId actorId) => _inner.GetOrder(actorId);

        public int NextOrder
        {
            get => _inner.NextOrder;
            set => _inner.NextOrder = value;
        }

        public void AttachWorld(WorldState world) => _inner.AttachWorld(world);
    }

    private sealed class SaveSandbox : IDisposable
    {
        private SaveSandbox(string directoryPath, DateTime timestamp)
        {
            DirectoryPath = directoryPath;
            Timestamp = timestamp;
        }

        public string DirectoryPath { get; }

        public DateTime Timestamp { get; }

        public Func<DateTime> Clock => () => Timestamp;

        public static SaveSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-determinism-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new SaveSandbox(directoryPath, new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc));
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
