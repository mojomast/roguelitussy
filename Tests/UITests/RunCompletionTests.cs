using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class RunCompletionTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.GameManager run completion contract ends at depth nine", FinalRunDepthIsNine);
        registry.Add("UI.GameManager act and boss transitions emit concise log feedback", ActAndBossTransitionsEmitFeedback);
        registry.Add("UI.GameManager final boss completion emits the canonical result once", FinalBossCompletionEmitsOnce);
        registry.Add("UI.GameManager completed run does not reward again after reentry", CompletionDoesNotRepeatAfterReentry);
        registry.Add("UI.GameManager shallow synthetic worlds do not complete the run", ShallowWorldDoesNotComplete);
        registry.Add("UI.Ascension enemy and starting HP modifiers affect runtime stats", AscensionModifiersAffectRuntimeStats);
        registry.Add("UI.MetaProgression first clear unlock is idempotent", FirstClearUnlockIsIdempotent);
    }

    private static void FinalRunDepthIsNine()
    {
        Expect.Equal(9, GameManager.FinalRunDepth, "The run contract should use the nine-floor, three-act depth.");

        var context = CreateContext(15, includeBoss: true);
        context.Manager.LoadWorld(context.World);
        var gameOver = 0;
        context.Bus.GameOver += (_, _) => gameOver++;

        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);

        Expect.Equal(0, gameOver, "Depth fifteen must no longer complete the run.");
        Expect.Equal(GameManager.GameState.Playing, context.Manager.CurrentState, "Depth fifteen should remain a playable synthetic floor.");
    }

    private static void ActAndBossTransitionsEmitFeedback()
    {
        var actContext = CreateContext(1, includeBoss: false);
        var actLogs = new List<string>();
        actContext.Bus.LogMessage += (message, _) => actLogs.Add(message);
        actContext.Manager.LoadWorld(actContext.World);
        Expect.True(actLogs.Contains("Act 1 begins."), "The first act should announce its entry through the log.");

        var bossContext = CreateContext(3, includeBoss: true);
        var bossLogs = new List<string>();
        bossContext.Bus.LogMessage += (message, _) => bossLogs.Add(message);
        bossContext.Manager.LoadWorld(bossContext.World);
        Expect.True(bossLogs.Contains("Act 1 boss: Boss."), "A boss floor should announce its boss through the log.");
    }

    private static void FinalBossCompletionEmitsOnce()
    {
        var context = CreateContext(GameManager.FinalRunDepth, includeBoss: true);
        context.Manager.LoadWorld(context.World);
        var gameOverWithStats = 0;
        var gameOver = 0;
        context.Bus.GameOverWithStats += stats => gameOverWithStats++;
        context.Bus.GameOver += (_, _) => gameOver++;

        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);
        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);

        Expect.Equal(1, gameOverWithStats, "A final boss clear should emit one stats result.");
        Expect.Equal(1, gameOver, "A final boss clear should emit one compatibility game-over result.");
        Expect.Equal(GameManager.GameState.GameOver, context.Manager.CurrentState, "A final boss clear should end the run.");
        Expect.Equal("Victory", context.Manager.CurrentRunStats.CauseOfDeath, "A successful clear should use the canonical victory cause.");
    }

    private static void ShallowWorldDoesNotComplete()
    {
        var context = CreateContext(1, includeBoss: false);
        context.Manager.LoadWorld(context.World);
        var gameOver = 0;
        context.Bus.GameOver += (_, _) => gameOver++;

        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);

        Expect.Equal(0, gameOver, "Synthetic shallow worlds must not be treated as final-boss clears.");
        Expect.Equal(GameManager.GameState.Playing, context.Manager.CurrentState, "A shallow floor clear should remain playable.");
    }

    private static void CompletionDoesNotRepeatAfterReentry()
    {
        var context = CreateContext(GameManager.FinalRunDepth, includeBoss: true);
        context.Manager.LoadWorld(context.World);
        var gameOver = 0;
        context.Bus.GameOver += (_, _) => gameOver++;

        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);
        context.Manager.LoadWorld(context.World);
        InvokePrivate(context.Manager, "TryResolveFloorClear", context.Player.Id);

        Expect.Equal(1, gameOver, "Re-entering a completed final floor must not emit another game-over result.");
    }

    private static void AscensionModifiersAffectRuntimeStats()
    {
        var content = new StubContentDatabase();
        ((Dictionary<string, AscensionModifier>)content.AscensionModifiers)["hp"] = new AscensionModifier
        {
            ModifierId = "hp",
            AscensionLevel = 1,
            EffectType = "enemy_stat_boost",
            EffectValue = 0.1f,
        };
        ((Dictionary<string, AscensionModifier>)content.AscensionModifiers)["start"] = new AscensionModifier
        {
            ModifierId = "start",
            AscensionLevel = 2,
            EffectType = "starting_hp_penalty",
            EffectValue = 10,
        };

        var enemyStats = new Stats { HP = 20, MaxHP = 20 };
        AscensionModifiers.ApplyEnemyStats(enemyStats, 1, content);
        Expect.Equal(22, enemyStats.MaxHP, "The enemy HP modifier should be applied deterministically.");
        Expect.Equal(22, enemyStats.HP, "Modified enemies should start at modified max HP.");
        Expect.Equal(10, AscensionModifiers.ResolveStartingHpPenalty(2, content), "The starting HP penalty should resolve at its ascension level.");
    }

    private static void FirstClearUnlockIsIdempotent()
    {
        var manager = new MetaProgressionManager();
        manager.CompleteRun();
        manager.CompleteRun();
        Expect.True(manager.HasCompletedFirstClear, "Completing a run should unlock ascension selection.");
    }

    private static (GameManager Manager, EventBus Bus, WorldState World, StubEntity Player) CreateContext(int depth, bool includeBoss)
    {
        var world = new WorldState { Depth = depth };
        world.InitGrid(8, 8);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 40, MaxHP = 40, Speed = 100, ViewRadius = 8 });
        world.Player = player;
        world.AddEntity(player);
        if (includeBoss)
        {
            var boss = new StubEntity("Boss", new Position(5, 5), Faction.Enemy,
                stats: new Stats { HP = 0, MaxHP = 10, Speed = 100 });
            boss.SetComponent(new EnemyComponent { TemplateId = "boss_test" });
            world.AddEntity(boss);
        }

        var bus = new EventBus();
        var manager = new GameManager();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);
        return (manager, bus, world, player);
    }

    private static void InvokePrivate(GameManager manager, string method, params object[] arguments)
    {
        var methodInfo = typeof(GameManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Expect.NotNull(methodInfo, $"GameManager should retain private method '{method}'.");
        methodInfo!.Invoke(manager, arguments);
    }
}
