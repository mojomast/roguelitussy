using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class CombatLogTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.CombatLog enemy action uses danger red", EnemyActionUsesDangerRed);
        registry.Add("UI.CombatLog loot uses bright gold", LootUsesBrightGold);
        registry.Add("UI.CombatLog critical is bold", CriticalUsesBold);
        registry.Add("UI.CombatLog old messages fade", OldMessagesFade);
        registry.Add("UI.EventBus empty log messages are safe", EmptyLogMessagesAreSafe);
        registry.Add("UI.CombatLog default filter indicator is All", DefaultFilterIndicatorIsAll);
        registry.Add("UI.CombatLog filter cycles through states and wraps", FilterCyclesThroughStatesAndWraps);
        registry.Add("UI.CombatLog long display messages are truncated", LongDisplayMessagesAreTruncated);
        registry.Add("UI.CombatLog combat filter includes warning combat grouping and excludes nonmatching categories", CombatFilterExcludesNonmatchingCategories);
        registry.Add("UI.CombatLog filtering preserves stored entries", FilteringPreservesStoredEntries);
        registry.Add("UI.CombatLog filter key routes through gameplay input", FilterKeyRoutesThroughInput);
        registry.Add("UI.CombatLog listens for progression feedback", ListensForProgressionFeedback);
    }

    private static void EnemyActionUsesDangerRed()
    {
        var markup = CombatLog.FormatEntryForTest("Goblin hits Rook.", LogCategory.EnemyAction, 0);

        Expect.True(markup.Contains(UiStyle.ToHex(UiStyle.DangerRed()), System.StringComparison.Ordinal),
            "Enemy log entries should use danger red.");
    }

    private static void LootUsesBrightGold()
    {
        var markup = CombatLog.FormatEntryForTest("Loot found: gold.", LogCategory.Loot, 0);

        Expect.True(markup.Contains(UiStyle.ToHex(UiStyle.BrightGold()), System.StringComparison.Ordinal),
            "Loot log entries should use bright gold.");
    }

    private static void CriticalUsesBold()
    {
        var markup = CombatLog.FormatEntryForTest("Rook dies.", LogCategory.Critical, 0);

        Expect.True(markup.Contains("[b]", System.StringComparison.Ordinal),
            "Critical log entries should be bold.");
    }

    private static void OldMessagesFade()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        for (var i = 0; i < 8; i++)
        {
            bus.EmitLogMessage($"message {i}");
        }

        Expect.True(log.RenderedText.Contains("99", System.StringComparison.Ordinal)
            || log.RenderedText.Contains("4d", System.StringComparison.Ordinal),
            "Older rendered log lines should include an alpha fade in their BBCode color.");
    }

    private static void EmptyLogMessagesAreSafe()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        bus.EmitLogMessage(null!);
        bus.EmitLogMessage(string.Empty);

        Expect.True(log.RenderedText is not null, "Null and empty messages should not throw during formatting.");
    }

    private static void DefaultFilterIndicatorIsAll()
    {
        var log = new CombatLog();

        Expect.True(log.RenderedText.Contains("[Filter: All]", System.StringComparison.Ordinal),
            "Combat log should render the default All filter indicator.");
    }

    private static void FilterCyclesThroughStatesAndWraps()
    {
        var log = new CombatLog();

        log.CycleFilter();
        Expect.True(log.RenderedText.Contains("[Filter: Combat]", System.StringComparison.Ordinal),
            "First cycle should select Combat filter.");

        log.CycleFilter();
        Expect.True(log.RenderedText.Contains("[Filter: Loot]", System.StringComparison.Ordinal),
            "Second cycle should select Loot filter.");

        log.CycleFilter();
        Expect.True(log.RenderedText.Contains("[Filter: System]", System.StringComparison.Ordinal),
            "Third cycle should select System filter.");

        log.CycleFilter();
        Expect.True(log.RenderedText.Contains("[Filter: All]", System.StringComparison.Ordinal),
            "Fourth cycle should wrap back to All filter.");
    }

    private static void LongDisplayMessagesAreTruncated()
    {
        var fitted = CombatLog.FormatDisplayMessageForTest(new string('x', 140));

        Expect.True(fitted.Length <= 96, "Combat log display lines should be capped before they can overrun the console panel.");
        Expect.True(fitted.EndsWith("...", System.StringComparison.Ordinal), "Truncated combat log lines should clearly show an ellipsis.");
    }

    private static void CombatFilterExcludesNonmatchingCategories()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        bus.EmitLogMessage("player strikes", LogCategory.PlayerAction);
        bus.EmitLogMessage("warning flash", LogCategory.Warning);
        bus.EmitLogMessage("loot sparkles", LogCategory.Loot);
        bus.EmitLogMessage("system hum", LogCategory.System);

        log.CycleFilter();

        Expect.True(log.RenderedText.Contains("player strikes", System.StringComparison.Ordinal),
            "Combat filter should include player action messages.");
        Expect.True(log.RenderedText.Contains("warning flash", System.StringComparison.Ordinal),
            "Combat filter intentionally includes Warning as combat/action-relevant feedback.");
        Expect.False(log.RenderedText.Contains("loot sparkles", System.StringComparison.Ordinal),
            "Combat filter should exclude loot messages.");
        Expect.False(log.RenderedText.Contains("system hum", System.StringComparison.Ordinal),
            "Combat filter should exclude system messages.");
    }

    private static void FilteringPreservesStoredEntries()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        bus.EmitLogMessage("loot survives filter", LogCategory.Loot);
        bus.EmitLogMessage("system survives filter", LogCategory.System);

        log.CycleFilter();
        Expect.False(log.RenderedText.Contains("loot survives filter", System.StringComparison.Ordinal),
            "Combat filter should hide loot messages without deleting them.");
        Expect.False(log.RenderedText.Contains("system survives filter", System.StringComparison.Ordinal),
            "Combat filter should hide system messages without deleting them.");

        log.CycleFilter();
        log.CycleFilter();
        log.CycleFilter();

        Expect.True(log.RenderedText.Contains("loot survives filter", System.StringComparison.Ordinal),
            "Cycling back to All should show previously hidden loot messages.");
        Expect.True(log.RenderedText.Contains("system survives filter", System.StringComparison.Ordinal),
            "Cycling back to All should show previously hidden system messages.");
    }

    private static void FilterKeyRoutesThroughInput()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        Expect.True(root.CombatLog.RenderedText.Contains("[Filter: All]", System.StringComparison.Ordinal),
            "Combat log should start on All through UIRoot wiring.");
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.L });

        Expect.True(root.CombatLog.RenderedText.Contains("[Filter: Combat]", System.StringComparison.Ordinal),
            "Gameplay filter key should cycle the combat log filter.");
    }

    private static void ListensForProgressionFeedback()
    {
        var context = CreateContext();
        var log = new CombatLog();
        log.Bind(context.GameManager, context.Bus);

        context.Bus.EmitExperienceGained(context.World.Player.Id, 5, 15);
        context.Bus.EmitLeveledUp(context.World.Player.Id, 2);
        context.Bus.EmitCurrencyChanged(context.World.Player.Id, 140);
        context.Bus.EmitKillStreakChanged(context.World.Player.Id, 2, 3);

        Expect.True(log.RenderedText.Contains("Gained 5 XP", System.StringComparison.Ordinal), "Combat log should show player XP gains from progression events.");
        Expect.True(log.RenderedText.Contains("Level up!", System.StringComparison.Ordinal), "Combat log should show level-up events.");
        Expect.True(log.RenderedText.Contains("Gold: 140", System.StringComparison.Ordinal), "Combat log should show player currency updates.");
        Expect.True(log.RenderedText.Contains("Kill streak: 2", System.StringComparison.Ordinal), "Combat log should show visible kill streak progression.");
    }

    private static UIContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                world.SetTile(position, TileType.Floor);
                world.SetVisible(position, true);
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
                ViewRadius = 6,
            });
        player.SetComponent(new InventoryComponent(20));

        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new UIContext(world, bus, gameManager, content);
    }

    private sealed record UIContext(WorldState World, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
