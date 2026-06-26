using System.Collections.Generic;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class TargetingOverlayTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.TargetingOverlay cursor starts at actor position", CursorStartsAtActorPosition);
        registry.Add("UI.TargetingOverlay confirm on invalid target returns null", ConfirmOnInvalidTargetReturnsNull);
        registry.Add("UI.TargetingOverlay cancel does not emit action", CancelDoesNotEmitAction);
        registry.Add("UI.TargetingOverlay preview returns correct aoe tiles", PreviewReturnsCorrectAoeTiles);
    }

    private static void CursorStartsAtActorPosition()
    {
        var context = CreateContext();
        var overlay = new TargetingOverlay();
        overlay.Bind(context.GameManager, context.Bus, context.Content);
        context.Content.TryGetAbilityTemplate("fireball", out var ability);

        overlay.EnterTargetingForItem(context.World, context.Player.Id, EntityId.New(), context.Content.ItemTemplates["scroll_fireball"], ability);

        Expect.True(overlay.IsActive, "Targeting should be active after entering mode.");
        Expect.Equal(context.Player.Position, overlay.CursorPosition, "Cursor should start at the actor's position.");
    }

    private static void ConfirmOnInvalidTargetReturnsNull()
    {
        var context = CreateContext();
        var overlay = new TargetingOverlay();
        overlay.Bind(context.GameManager, context.Bus, context.Content);
        context.Content.TryGetAbilityTemplate("fireball", out var ability);

        overlay.EnterTargetingForItem(context.World, context.Player.Id, EntityId.New(), context.Content.ItemTemplates["scroll_fireball"], ability);
        overlay.MoveCursor(new Position(9, 0));

        var action = overlay.Confirm();
        Expect.True(action is null, "Confirm should return null when the cursor is outside ability range.");
    }

    private static void CancelDoesNotEmitAction()
    {
        var context = CreateContext();
        var overlay = new TargetingOverlay();
        overlay.Bind(context.GameManager, context.Bus, context.Content);
        context.Content.TryGetAbilityTemplate("fireball", out var ability);

        overlay.EnterTargetingForItem(context.World, context.Player.Id, EntityId.New(), context.Content.ItemTemplates["scroll_fireball"], ability);

        IAction? emitted = null;
        context.Bus.PlayerActionSubmitted += action => emitted = action;

        overlay.Cancel();

        Expect.True(emitted is null, "Cancel should not emit a player action.");
        Expect.False(overlay.IsActive, "Targeting should exit after cancel.");
    }

    private static void PreviewReturnsCorrectAoeTiles()
    {
        var context = CreateContext();
        var overlay = new TargetingOverlay();
        overlay.Bind(context.GameManager, context.Bus, context.Content);
        context.Content.TryGetAbilityTemplate("fireball", out var ability);

        overlay.EnterTargetingForItem(context.World, context.Player.Id, EntityId.New(), context.Content.ItemTemplates["scroll_fireball"], ability);
        overlay.MoveCursor(new Position(10, 10));

        var preview = new HashSet<Position>(overlay.PreviewTiles);
        var expected = new HashSet<Position>();
        var radius = ability.Targeting.Radius;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                expected.Add(new Position(10 + dx, 10 + dy));
            }
        }

        Expect.Equal(expected.Count, preview.Count, "Fireball preview should cover every tile in the Chebyshev radius.");
        foreach (var position in expected)
        {
            Expect.True(preview.Contains(position), $"Preview should include tile {position}.");
        }
    }

    private static TargetingContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(20, 20);
        world.Depth = 1;
        world.Seed = 42;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity(
            "Player",
            new Position(10, 10),
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
        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new TargetingContext(world, player, bus, gameManager, content);
    }

    private sealed record TargetingContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
