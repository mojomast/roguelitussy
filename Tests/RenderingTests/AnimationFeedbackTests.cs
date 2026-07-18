using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.RenderingTests;

public sealed class AnimationFeedbackTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.Attack animation lunges toward the target and returns", AttackAnimationLungesAndReturns);
        registry.Add("Rendering.Kill defers sprite removal until the death animation completes", KillDefersSpriteRemovalUntilDeathAnimationCompletes);
        registry.Add("Rendering.CompleteAll finishes pending death animations and frees sprites", CompleteAllFinishesPendingDeathAnimations);
        registry.Add("Rendering.Damage popups style crits heals and misses distinctly", DamagePopupsStyleCritsHealsAndMisses);
        registry.Add("Rendering.Damage popup fade preserves its base color", DamagePopupFadePreservesBaseColor);
        registry.Add("Rendering.Negative damage routes to heal popup styling", NegativeDamageRoutesToHealPopup);
        registry.Add("Rendering.Healed event spawns a green heal popup", HealedEventSpawnsHealPopup);
        registry.Add("Rendering.Item pickup spawns a named pickup popup", ItemPickupSpawnsNamedPopup);
    }

    private static void AttackAnimationLungesAndReturns()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);
        var view = CreateWorldView(world, bus);

        var attackerSprite = view.EntityRenderer.GetSprite(world.Player.Id)!;
        var restingPosition = attackerSprite.Position;

        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 5, 5, DamageType.Physical, false, false, false));

        Expect.True(view.Animations.IsAttackAnimating(world.Player.Id), "Attack should register an active lunge animation.");

        view._Process(0.05d);
        var lungePosition = attackerSprite.Position;
        Expect.True(lungePosition.X > restingPosition.X,
            "During the first half of the attack the sprite should lunge toward the target.");

        view._Process(0.30d);
        Expect.Equal(restingPosition, attackerSprite.Position, "The attack should return the sprite to its resting position.");
        Expect.Equal(0, view.Animations.ActiveAttackCount, "The finished attack should be removed from the active set.");
    }

    private static void KillDefersSpriteRemovalUntilDeathAnimationCompletes()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);
        world.SetVisible(enemy.Position, true);
        var view = CreateWorldView(world, bus);

        var enemySprite = view.EntityRenderer.GetSprite(enemy.Id)!;
        Expect.True(enemySprite.Visible, "Precondition: the enemy sprite should be visible before it dies.");

        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 9, 9, DamageType.Physical, false, false, IsKill: true));

        Expect.False(view.EntityRenderer.HasSprite(enemy.Id), "The killed entity should leave the live sprite registry immediately.");
        Expect.True(view.Animations.IsDeathAnimating(enemy.Id), "A kill should start a death animation instead of freeing instantly.");
        Expect.True(view.EntityLayerNode.GetChildren().Contains(enemySprite),
            "The dying sprite should stay in the tree while the fade plays.");

        view._Process(0.10d);
        Expect.True(enemySprite.Modulate.A < 1f, "The dying sprite should fade out over time.");
        Expect.True(view.EntityLayerNode.GetChildren().Contains(enemySprite),
            "The dying sprite should still be in the tree mid-animation.");

        view._Process(0.50d);
        Expect.Equal(0, view.Animations.ActiveDeathCount, "The death animation should complete.");
        Expect.False(view.EntityLayerNode.GetChildren().Contains(enemySprite),
            "The sprite should be removed from the layer once the death animation completes.");
    }

    private static void CompleteAllFinishesPendingDeathAnimations()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);
        world.SetVisible(enemy.Position, true);
        var view = CreateWorldView(world, bus);

        var enemySprite = view.EntityRenderer.GetSprite(enemy.Id)!;
        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 9, 9, DamageType.Physical, false, false, IsKill: true));
        Expect.True(view.Animations.IsDeathAnimating(enemy.Id), "Precondition: death animation should be in flight.");

        view.Animations.CompleteAll();

        Expect.Equal(0, view.Animations.ActiveDeathCount, "CompleteAll should drain pending death animations.");
        Expect.False(view.EntityLayerNode.GetChildren().Contains(enemySprite),
            "CompleteAll should free the dying sprite so full re-renders start clean.");
    }

    private static void DamagePopupsStyleCritsHealsAndMisses()
    {
        var normal = new DamagePopup();
        normal.Setup(7, isCrit: false, isHeal: false);
        Expect.Equal("7", normal.Text, "Normal hits should render the plain amount.");
        Expect.Equal(DamagePopup.NormalColor, normal.BaseColor, "Normal hits should stay white.");

        var crit = new DamagePopup();
        crit.Setup(12, isCrit: true, isHeal: false);
        Expect.Equal("12!", crit.Text, "Crits should render with an exclamation.");
        Expect.Equal(DamagePopup.CriticalColor, crit.BaseColor, "Crits should use the orange critical color.");

        var heal = new DamagePopup();
        heal.Setup(5, isCrit: false, isHeal: true);
        Expect.Equal("+5", heal.Text, "Heals should render as +N.");
        Expect.Equal(DamagePopup.HealColor, heal.BaseColor, "Heals should use the green heal color.");

        var miss = new DamagePopup();
        miss.Setup(0, isCrit: false, isHeal: false, isMiss: true);
        Expect.Equal("MISS", miss.Text, "Misses should render MISS.");
        Expect.Equal(DamagePopup.MissColor, miss.BaseColor, "Misses should use the gray miss color.");

        Expect.Equal(crit.BaseColor, crit.Modulate, "Setup should apply the base color to the label tint.");
    }

    private static void DamagePopupFadePreservesBaseColor()
    {
        var controller = new AnimationController();
        var parent = new Node2D();
        controller.SpawnDamagePopup(parent, Vector2.Zero, 5, isCrit: false, isHeal: true);
        var popup = parent.GetChildren().OfType<DamagePopup>().Single();

        controller.Advance(DamagePopup.Duration * 0.5d);

        Expect.Equal(DamagePopup.HealColor.R, popup.Modulate.R, "Fading should keep the heal tint's red channel.");
        Expect.Equal(DamagePopup.HealColor.G, popup.Modulate.G, "Fading should keep the heal tint's green channel.");
        Expect.Equal(DamagePopup.HealColor.B, popup.Modulate.B, "Fading should keep the heal tint's blue channel.");
        Expect.True(popup.Modulate.A < 1f, "Fading should reduce only the popup alpha.");
    }

    private static void NegativeDamageRoutesToHealPopup()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);
        var view = CreateWorldView(world, bus);

        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, -6, -6, DamageType.Physical, false, false, false));

        var popup = view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Single();
        Expect.True(popup.IsHeal, "Negative final damage should render with heal styling.");
        Expect.Equal("+6", popup.Text, "Negative final damage should show the restored amount as +N.");
    }

    private static void HealedEventSpawnsHealPopup()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var view = CreateWorldView(world, bus);

        bus.EmitHealed(world.Player.Id, 7);

        var popup = view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Single();
        Expect.True(popup.IsHeal, "Healed events should spawn heal-styled popups.");
        Expect.Equal("+7", popup.Text, "Heal popups should show the healed amount as +N.");
        Expect.Equal(DamagePopup.HealColor, popup.BaseColor, "Heal popups should use the green heal color.");

        bus.EmitHealed(world.Player.Id, 0);
        Expect.Equal(1, view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Count(),
            "Zero-amount heals should not spawn popups.");
    }

    private static void ItemPickupSpawnsNamedPopup()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var view = CreateWorldView(world, bus);

        bus.EmitItemPickedUp(world.Player.Id, new ItemInstance { TemplateId = "potion_health" });

        var popup = view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Single();
        Expect.Equal("potion health", popup.Text,
            "Pickup popups should show the item name (template id fallback without underscores).");
        Expect.Equal(RenderPalette.PickupPopup, popup.BaseColor, "Pickup popups should use the shared pickup tint.");
    }

    private static WorldView CreateWorldView(WorldState world, EventBus? bus = null)
    {
        var view = new WorldView();
        view.BindWorld(world);
        view.BindEventBus(bus);
        return view;
    }

    private static WorldState CreateRoomWorld(int width = 7, int height = 7, Position? playerPosition = null)
    {
        var world = new WorldState();
        world.InitGrid(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var tile = x == 0 || y == 0 || x == width - 1 || y == height - 1
                    ? TileType.Wall
                    : TileType.Floor;
                world.SetTile(new Position(x, y), tile);
            }
        }

        var player = new StubEntity(
            "Player",
            playerPosition ?? new Position(2, 2),
            Faction.Player,
            stats: new Stats
            {
                HP = 20,
                MaxHP = 20,
                Attack = 5,
                Defense = 2,
                Accuracy = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });

        world.Player = player;
        world.AddEntity(player);
        return world;
    }
}
