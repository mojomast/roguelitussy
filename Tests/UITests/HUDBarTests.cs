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
}
