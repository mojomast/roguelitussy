using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class AbilityPaletteTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Ability palette lists only owned stable abilities", PaletteListsOwnedAbilities);
        registry.Add("UI.Ability palette routes self and aimed casts without input leak", PaletteRoutesSelfAndAimedCasts);
    }

    private static void PaletteListsOwnedAbilities()
    {
        var context = CreateContext();
        context.Player.GetComponent<AbilitiesComponent>()!.Slots.Add(new EnemyAbilitySlot { AbilityId = "phase_shift" });
        context.Player.GetComponent<AbilitiesComponent>()!.Slots.Add(new EnemyAbilitySlot { AbilityId = "phase_shift" });
        context.Player.GetComponent<CooldownComponent>()!.SetCooldown("phase_shift", 2);
        var palette = new AbilityPalette();
        palette.Bind(context.Manager, context.Bus, context.Content, context.Targeting);

        Expect.True(palette.OpenForPlayer(), "Owned abilities should open the palette.");
        Expect.Equal(1, palette.ListedAbilities.Count, "Duplicate slots should list once.");
        Expect.Equal("phase_shift", palette.ListedAbilities[0].AbilityId, "Only owned abilities may be listed.");
        Expect.True(palette.Options[0].Contains("CD 2"), "Cooldown abilities should be visibly disabled.");
    }

    private static void PaletteRoutesSelfAndAimedCasts()
    {
        var context = CreateContext();
        var abilities = context.Player.GetComponent<AbilitiesComponent>()!;
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "phase_shift" });
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "arrow_shot" });
        var palette = new AbilityPalette();
        palette.Bind(context.Manager, context.Bus, context.Content, context.Targeting);
        IAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action;

        Expect.True(palette.OpenForPlayer(), "Palette should open for owned abilities.");
        Expect.True(palette.HandleKey(Key.Key1), "Self ability hotkey should be handled.");
        Expect.True(submitted is CastAbilityAction, "Self ability should use the normal action event path.");
        Expect.False(palette.Visible, "Self ability should close the palette after submitting one action.");

        submitted = null;
        Expect.True(palette.OpenForPlayer(), "Palette should reopen without spending a turn.");
        Expect.True(palette.HandleKey(Key.Key2), "Aimed ability hotkey should be handled.");
        Expect.True(context.Targeting.IsActive, "Aimed ability should enter the existing targeting overlay.");
        context.Targeting.HandleKey(Key.Escape);
        Expect.False(context.Targeting.IsActive, "Cancelling targeting should consume no action.");
        Expect.True(submitted is null, "Cancelled targeting must not submit an action.");
    }

    private static (GameManager Manager, EventBus Bus, StubContentDatabase Content, WorldState World, Entity Player, TargetingOverlay Targeting) CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity("Player", new Position(2, 2), new Stats { HP = 20, MaxHP = 20, Attack = 5, Speed = 100 }, Faction.Player);
        player.SetComponent(new AbilitiesComponent());
        player.SetComponent(new CooldownComponent());
        player.SetComponent(new IdentityComponent { RaceId = "elf" });
        world.Player = player;
        world.AddEntity(player);
        var manager = new GameManager();
        var bus = new EventBus();
        var content = new StubContentDatabase();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        manager.LoadWorld(world);
        var targeting = new TargetingOverlay();
        targeting.Bind(manager, bus, content);
        return (manager, bus, content, world, player, targeting);
    }
}
