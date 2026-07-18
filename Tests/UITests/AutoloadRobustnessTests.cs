using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class AutoloadRobustnessTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.GameManager status-tick death emits canonical game-over pair exactly once", StatusTickDeathEmitsCanonicalGameOverOnce);
        registry.Add("UI.GameManager game-over stats capture relics, archetype, synergies, and perks", GameOverStatsCaptureRunBuild);
        registry.Add("UI.DailyChallengeManager survives a corrupt persistence file", DailyChallengeManagerSurvivesCorruptFile);
        registry.Add("UI.FloorEventPopupUI rebind does not stack event handlers", FloorEventPopupRebindDoesNotStackHandlers);
    }

    private static void StatusTickDeathEmitsCanonicalGameOverOnce()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);

        var withStatsCount = 0;
        var simpleCount = 0;
        RunStats? capturedStats = null;
        context.Bus.GameOverWithStats += stats =>
        {
            withStatsCount++;
            capturedStats = stats;
        };
        context.Bus.GameOver += (_, _) => simpleCount++;

        context.Player.Stats.HP = 5;
        Expect.True(
            StatusEffectProcessor.ApplyEffect(context.Player, StatusEffectType.Poisoned, duration: 3, magnitude: 10),
            "Test setup should be able to poison the player.");

        context.GameManager.ProcessPlayerAction(new WaitAction(context.Player.Id));

        Expect.Equal(1, withStatsCount, "A status-tick death should emit GameOverWithStats exactly once.");
        Expect.Equal(1, simpleCount, "A status-tick death should emit GameOver exactly once.");
        Expect.Equal(GameManager.GameState.GameOver, context.GameManager.CurrentState, "A status-tick death should transition the game state to GameOver.");
        Expect.True(capturedStats is not null && capturedStats.CauseOfDeath.Contains("Poison", StringComparison.OrdinalIgnoreCase),
            "A poison death should report poison as the cause of death.");

        context.GameManager.ProcessPlayerAction(new WaitAction(context.Player.Id));

        Expect.Equal(1, withStatsCount, "Further actions after death should not re-emit GameOverWithStats.");
        Expect.Equal(1, simpleCount, "Further actions after death should not re-emit GameOver.");
    }

    private static void GameOverStatsCaptureRunBuild()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);

        var relics = new RelicComponent();
        relics.AddRelic("iron_pact");
        context.Player.SetComponent(relics);
        context.Player.SetComponent(new ArchetypeComponent { ArchetypeId = "vanguard" });
        var progression = new ProgressionComponent();
        progression.SelectedPerkIds.Add("iron_will");
        context.Player.SetComponent(progression);

        RunStats? capturedStats = null;
        context.Bus.GameOverWithStats += stats => capturedStats = stats;

        context.Player.Stats.HP = 1;
        StatusEffectProcessor.ApplyEffect(context.Player, StatusEffectType.Poisoned, duration: 2, magnitude: 5);
        context.GameManager.ProcessPlayerAction(new WaitAction(context.Player.Id));

        Expect.NotNull(capturedStats, "Killing the player should emit run stats.");
        Expect.Equal("vanguard", capturedStats!.Archetype, "Run stats should capture the player's archetype.");
        Expect.True(capturedStats.RelicIds?.Contains("iron_pact") == true, "Run stats should capture held relics.");
        Expect.NotNull(capturedStats.SynergyIds, "Run stats should carry a non-null active-synergy collection.");
        Expect.True(capturedStats.PerkIds?.Contains("iron_will") == true, "Run stats should capture selected perks.");
        Expect.NotNull(capturedStats.FloorsClearedThemes, "Run stats should carry a non-null cleared-floors collection.");
    }

    private static void DailyChallengeManagerSurvivesCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daily_corrupt_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json !!");

            var manager = new DailyChallengeManager();
            manager.LoadFromFile(path);

            Expect.False(manager.TodayAttempted, "A corrupt daily-challenge file should fall back to a fresh day.");
            Expect.Equal(0, manager.TodayBestFloor, "A corrupt daily-challenge file should reset best floor.");

            manager.SaveToFile(path);
            Expect.True(File.Exists(path), "Saving after recovery should produce a persistence file.");
            var parsed = JsonSerializer.Deserialize<DailyChallengeData>(File.ReadAllText(path));
            Expect.NotNull(parsed, "The recovered save should contain valid JSON.");
            Expect.False(File.Exists(path + ".tmp"), "The atomic write should not leave a temp file behind.");

            var reloaded = new DailyChallengeManager();
            reloaded.LoadFromFile(path);
            Expect.False(reloaded.TodayAttempted, "Reloading the recovered save should not throw or corrupt state.");
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".tmp");
        }
    }

    private static void FloorEventPopupRebindDoesNotStackHandlers()
    {
        var popup = new FloorEventPopupUI();
        var firstBus = new EventBus();

        popup.Bind(firstBus);
        popup.Bind(firstBus);

        Expect.Equal(1, CountSubscribers(firstBus, "CurseRoomEntered"), "Rebinding the same bus should not stack curse-room handlers.");
        Expect.Equal(1, CountSubscribers(firstBus, "LevelTransition"), "Rebinding the same bus should not stack level-transition handlers.");

        var secondBus = new EventBus();
        popup.Bind(secondBus);

        Expect.Equal(0, CountSubscribers(firstBus, "CurseRoomEntered"), "Binding a new bus should unsubscribe from the previous bus.");

        firstBus.EmitCurseRoomEntered();
        Expect.True(string.IsNullOrEmpty(popup.MessageText), "Events from a previously bound bus should not reach the popup.");

        secondBus.EmitCurseRoomEntered();
        Expect.True(popup.MessageText.Contains("Dark power", StringComparison.Ordinal), "Events from the currently bound bus should show the popup message.");

        secondBus.EmitLevelTransition(1, 2);
        Expect.True(popup.MessageText.Contains("floor 2", StringComparison.Ordinal), "Floor transitions should show a floor-change banner.");
    }

    private static int CountSubscribers(EventBus bus, string eventName)
    {
        var field = typeof(EventBus).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        Expect.NotNull(field, $"EventBus should expose a field-like event named {eventName}.");
        return field!.GetValue(bus) is Delegate handler ? handler.GetInvocationList().Length : 0;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static UIContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity(
            "Player",
            new Position(1, 1),
            Faction.Player,
            stats: new Stats
            {
                HP = 40,
                MaxHP = 40,
                Attack = 8,
                Defense = 4,
                Accuracy = 80,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });
        player.SetComponent(new InventoryComponent(20));
        player.SetComponent(new WalletComponent { Gold = 120 });

        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new UIContext(world, player, bus, gameManager, content);
    }

    private sealed record UIContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
