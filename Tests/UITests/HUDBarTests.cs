using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class HUDBarTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.HUD HP damage keeps displayed value above target before ticks", HpDamageKeepsDisplayedValueBeforeTicks);
        registry.Add("UI.HUD HP healing advances displayed value upward", HpHealingAdvancesDisplayedValueUpward);
        registry.Add("UI.HUD HP display converges after repeated process ticks", HpDisplayConvergesAfterTicks);
        registry.Add("UI.HUD HP ghost trails damage and clears on healing", HpGhostTrailsDamageAndClearsOnHealing);
        registry.Add("UI.HUD HP danger pattern appears below thirty percent", HpDangerPatternAppearsBelowThirtyPercent);
        registry.Add("UI.HUD HP danger pattern clears above thirty percent", HpDangerPatternClearsAboveThirtyPercent);
        registry.Add("UI.HUD energy ghost trails spending", EnergyGhostTrailsSpending);
        registry.Add("UI.HUD bottom status shows HP and XP above hotbar", BottomStatusShowsHpAndXpAboveHotbar);
        registry.Add("UI.HUD dynamic text rows fit their allocated bounds", DynamicTextRowsFitAllocatedBounds);
        registry.Add("UI.HUD shows relic tray boss and kill streak indicators", ShowsRelicBossAndKillStreakIndicators);
        registry.Add("UI.HUD shows latest action feedback prominently", ShowsLatestActionFeedbackProminently);
    }

    private static void HpDamageKeepsDisplayedValueBeforeTicks()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Player.Stats.HP = 18;
        context.Bus.EmitHPChanged(context.Player.Id, 18, 40);

        Expect.Equal(18d, hud.HPBarValue, "HP target should decrease immediately after damage.");
        Expect.True(hud.HPBarDisplayedValue > hud.HPBarValue, "Displayed HP should remain above the lower damage target before process ticks.");
    }

    private static void HpHealingAdvancesDisplayedValueUpward()
    {
        var context = CreateContext(hp: 20, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Player.Stats.HP = 32;
        context.Bus.EmitHPChanged(context.Player.Id, 32, 40);

        var before = hud.HPBarDisplayedValue;
        hud._Process(0.05d);

        Expect.Equal(32d, hud.HPBarValue, "HP target should increase immediately after healing.");
        Expect.True(hud.HPBarDisplayedValue > before, "Displayed HP should advance upward after a healing process tick.");
        Expect.True(hud.HPBarDisplayedValue < hud.HPBarValue, "Displayed HP should still be interpolating before convergence.");
    }

    private static void HpDisplayConvergesAfterTicks()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Player.Stats.HP = 12;
        context.Bus.EmitHPChanged(context.Player.Id, 12, 40);

        for (var i = 0; i < 24; i++)
        {
            hud._Process(0.05d);
        }

        Expect.Equal(12d, hud.HPBarDisplayedValue, "Displayed HP should converge to the target after repeated process ticks.");
    }

    private static void HpGhostTrailsDamageAndClearsOnHealing()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Player.Stats.HP = 16;
        context.Bus.EmitHPChanged(context.Player.Id, 16, 40);
        hud._Process(0.05d);

        Expect.True(hud.HPBarGhostValue > hud.HPBarDisplayedValue, "Damage should leave a ghost HP trail above the faster main fill.");
        Expect.True(hud.HPBarGhostValue > hud.HPBarValue, "Damage ghost should start above the new lower target.");

        var ghostBefore = hud.HPBarGhostValue;
        for (var i = 0; i < 18; i++)
        {
            hud._Process(0.05d);
        }

        Expect.True(hud.HPBarGhostValue < ghostBefore, "HP damage ghost should converge downward over time.");

        context.Player.Stats.HP = 34;
        context.Bus.EmitHPChanged(context.Player.Id, 34, 40);

        Expect.True(hud.HPBarGhostValue <= hud.HPBarDisplayedValue + 0.001d, "Healing should not leave a misleading damage trail above the main HP fill.");
    }

    private static void EnergyGhostTrailsSpending()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Scheduler.ConsumeEnergy(context.Player.Id, 300);
        context.Bus.EmitTurnCompleted();
        hud._Process(0.05d);

        Expect.Equal(500d, hud.EnergyBarValue, "Energy target should decrease after spending scheduler energy.");
        Expect.True(hud.EnergyBarDisplayedValue > hud.EnergyBarValue, "Displayed energy should remain above the lower target during interpolation.");
        Expect.True(hud.EnergyBarGhostValue > hud.EnergyBarDisplayedValue, "Energy spending should leave a subtle trailing ghost above the main fill.");
    }

    private static void HpDangerPatternAppearsBelowThirtyPercent()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Player.Stats.HP = 11;
        context.Bus.EmitHPChanged(context.Player.Id, 11, 40);

        Expect.True(hud.HPBarDangerPatternVisible, "HP below thirty percent should expose the non-color danger pattern state.");
    }

    private static void HpDangerPatternClearsAboveThirtyPercent()
    {
        var context = CreateContext(hp: 8, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        Expect.True(hud.HPBarDangerPatternVisible, "Initial low HP should expose the danger pattern state.");

        context.Player.Stats.HP = 20;
        context.Bus.EmitHPChanged(context.Player.Id, 20, 40);

        Expect.False(hud.HPBarDangerPatternVisible, "Healing above thirty percent should clear the non-color danger pattern state.");
    }

    private static void BottomStatusShowsHpAndXpAboveHotbar()
    {
        WithViewportSize(new Vector2(1280f, 720f), viewportSize =>
        {
            var context = CreateContext(hp: 24, maxHp: 40, energy: 800);
            context.Player.SetComponent(new ProgressionComponent { Level = 2, Experience = 18, ExperienceToNextLevel = 30 });
            var root = new Control();
            var hud = new HUD();
            root.AddChild(hud);
            hud.Bind(context.GameManager, context.Bus);

            var bottomPanel = FindChild<Panel>(hud, "BottomStatusPanel");
            var hpLabel = bottomPanel is null ? null : FindChild<Label>(bottomPanel, "BottomHPLabel");
            var xpLabel = bottomPanel is null ? null : FindChild<Label>(bottomPanel, "BottomXPLabel");

            Expect.NotNull(bottomPanel, "HUD should create a bottom status strip for glanceable combat state.");
            Expect.True(bottomPanel!.Position.Y + bottomPanel.Size.Y <= viewportSize.Y - 54f - 12f - 7.9f, "Bottom HP/XP strip should sit directly above the quick-use hotbar lane.");
            Expect.Equal("HP: 24/40", hpLabel!.Text, "Bottom status strip should mirror current HP.");
            Expect.True(xpLabel!.Text.Contains("XP: 18/30", System.StringComparison.Ordinal), "Bottom status strip should show XP progress text.");
            Expect.Equal(18d, hud.XPBarValue, "HUD should expose XP bar progress for tests and UI layout.");
            Expect.Equal(30d, hud.XPBarMaxValue, "HUD should expose XP bar max for tests and UI layout.");
        });
    }

    private static void ShowsRelicBossAndKillStreakIndicators()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        context.Player.SetComponent(new RelicComponent());
        context.Player.GetComponent<RelicComponent>()!.RelicIds.Add("vampire_fang");
        context.Player.SetComponent(new KillStreakComponent { CurrentStreak = 2, HighestStreak = 3 });
        var hud = CreateHud(context);

        context.Bus.EmitRelicsChanged(context.Player.Id, context.GameManager.GetPlayerRelics());
        context.Bus.EmitKillStreakChanged(context.Player.Id, 2, 3);

        var boss = new StubEntity("Boss Guardian", new Position(1, 1), Faction.Enemy, stats: new Stats { HP = 30, MaxHP = 40, Attack = 8, Defense = 2, Accuracy = 80, Evasion = 5, Speed = 100 });
        boss.SetComponent(new EnemyComponent { TemplateId = "boss_stone_guardian" });
        context.GameManager.World!.AddEntity(boss);
        context.Bus.EmitBossRoomEntered(boss.Id);

        Expect.True(hud.RelicTrayText.Contains("Vampire Fang"), "HUD should show claimed relic names in the relic tray text.");
        Expect.True(hud.KillStreakText.Contains("2"), "HUD should show Trickster kill streaks at two or more kills.");
        Expect.True(hud.BossHealthText.Contains("Boss Guardian"), "HUD should show boss health after BossRoomEntered.");
        Expect.True(hud.Snapshot().Contains("Relics:"), "HUD snapshot should include the relic tray when relics exist.");
    }

    private static void ShowsLatestActionFeedbackProminently()
    {
        var context = CreateContext(hp: 40, maxHp: 40, energy: 800);
        var hud = CreateHud(context);

        context.Bus.EmitActionFeedback(new ActionFeedbackEventArgs(
            "You hit Skeleton for 4 damage.",
            LogCategory.PlayerAction,
            ActionResult.Success,
            ActionType.MeleeAttack,
            false));

        var feedbackLabel = FindChild<Label>(hud, "ActionFeedbackLabel");
        Expect.Equal("You hit Skeleton for 4 damage.", hud.ActionFeedbackText, "HUD should expose the latest action feedback for smoke tests and UI state.");
        Expect.NotNull(feedbackLabel, "HUD should create a prominent action feedback label.");
        Expect.True(feedbackLabel!.Visible, "HUD action feedback label should become visible after feedback arrives.");
        Expect.True(hud.Snapshot().Contains("You hit Skeleton", System.StringComparison.Ordinal), "HUD snapshot should include latest action feedback.");
    }

    private static void DynamicTextRowsFitAllocatedBounds()
    {
        WithViewportSize(new Vector2(640f, 360f), _ =>
        {
            var context = CreateContext(hp: 1234, maxHp: 9999, energy: 800);
            context.Player.SetComponent(new ProgressionComponent { Level = 12, Experience = 12345, ExperienceToNextLevel = 98765, UnspentStatPoints = 9, UnspentPerkChoices = 3 });
            context.Player.SetComponent(new KillStreakComponent { CurrentStreak = 22, HighestStreak = 99 });
            var hud = CreateHud(context);
            hud.SetInteractionPrompt("Press F to inspect the extremely wordy ancient contraption before it collapses.");

            var panel = (Panel)hud.Children[0];
            var mapLabel = FindChild<Label>(panel, "MapLabel");
            var statusIcons = FindChild<HBoxContainer>(panel, "StatusIconsContainer");
            var hotbarLabel = FindChild<Label>(panel, "HotbarLabel");
            var bottomPanel = FindChild<Panel>(hud, "BottomStatusPanel");
            var bottomHp = bottomPanel is null ? null : FindChild<Label>(bottomPanel, "BottomHPLabel");
            var bottomXp = bottomPanel is null ? null : FindChild<Label>(bottomPanel, "BottomXPLabel");
            var bottomHpBar = bottomPanel is null ? null : FindChild<ColorRect>(bottomPanel, "BottomHPBarBackground");

            Expect.NotNull(mapLabel, "HUD should expose a map label for layout checks.");
            Expect.NotNull(statusIcons, "HUD should expose a status icon row for layout checks.");
            Expect.NotNull(hotbarLabel, "HUD should expose a hotbar label for layout checks.");
            Expect.False(Overlaps(statusIcons!, mapLabel!), "Status icon row and map label should not overlap vertically.");
            Expect.False(Overlaps(hotbarLabel!, statusIcons!), "Hotbar label and status icon row should not overlap vertically.");
            Expect.NotNull(bottomHp, "Bottom HP label should exist.");
            Expect.NotNull(bottomXp, "Bottom XP label should exist.");
            Expect.NotNull(bottomHpBar, "Bottom HP bar should exist.");
            Expect.True(bottomHp!.Position.X + bottomHp.Size.X <= bottomHpBar!.Position.X + 0.1f, "Bottom HP text should end before the HP bar begins.");
            Expect.True(bottomXp!.Text.Length <= 18, "Bottom XP text should use a compact label that fits beside the bar.");
        });
    }

    private static HUD CreateHud(HudBarContext context)
    {
        var hud = new HUD();
        hud.Bind(context.GameManager, context.Bus);
        return hud;
    }

    private static HudBarContext CreateContext(int hp, int maxHp, int energy)
    {
        var bus = new EventBus();
        var content = new StubContentDatabase();
        var world = new WorldState();
        world.InitGrid(5, 5);
        world.Depth = 1;
        world.Seed = 1234;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity(
            "Player",
            new Position(2, 2),
            Faction.Player,
            stats: new Stats { HP = hp, MaxHP = maxHp, Attack = 8, Defense = 2, Accuracy = 80, Evasion = 5, Speed = 100, Energy = energy });
        player.SetComponent(new InventoryComponent(10));
        world.Player = player;
        world.AddEntity(player);

        var scheduler = new TurnScheduler();
        scheduler.Register(player);
        var gameManager = new GameManager();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        return new HudBarContext(gameManager, bus, scheduler, player);
    }

    private sealed record HudBarContext(GameManager GameManager, EventBus Bus, TurnScheduler Scheduler, StubEntity Player);

    private static T? FindChild<T>(Node parent, string name) where T : Node
    {
        foreach (var child in parent.Children)
        {
            if (child is T typed && typed.Name == name)
            {
                return typed;
            }
        }

        return null;
    }

    private static bool Overlaps(Control left, Control right)
    {
        return left.Position.X < right.Position.X + right.Size.X
            && left.Position.X + left.Size.X > right.Position.X
            && left.Position.Y < right.Position.Y + right.Size.Y
            && left.Position.Y + left.Size.Y > right.Position.Y;
    }

    private static void WithViewportSize(Vector2 viewportSize, System.Action<Vector2> action)
    {
        var viewport = new Control().GetViewport();
        var originalSize = viewport.Size;
        viewport.Size = viewportSize;

        try
        {
            action(viewportSize);
        }
        finally
        {
            viewport.Size = originalSize;
        }
    }
}
