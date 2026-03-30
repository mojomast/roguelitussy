using System;
using Xunit;
using Roguelike.Core;
using Roguelike.Core.Simulation;
using Roguelike.Tests.Stubs;

namespace Roguelike.Tests;

public class ActionTests
{
    [Fact]
    public void MoveAction_Validate_SucceedsForAdjacentFloor()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();
        var target = new Position(5, 4); // adjacent floor tile
        var action = new MoveAction(player.Id, target);

        Assert.Equal(ActionResult.Success, action.Validate(world));
    }

    [Fact]
    public void MoveAction_Validate_BlockedByWall()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();
        // Move player to (1,1) first so wall at (0,1) is adjacent
        player.Position = new Position(1, 1);
        world.UpdateEntityPosition(player.Id, new Position(5, 5), new Position(1, 1));

        var action = new MoveAction(player.Id, new Position(0, 1)); // wall
        Assert.Equal(ActionResult.Blocked, action.Validate(world));
    }

    [Fact]
    public void MoveAction_Execute_UpdatesPosition()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();
        var target = new Position(5, 4);
        var action = new MoveAction(player.Id, target);

        var outcome = action.Execute(world);

        Assert.Equal(ActionResult.Success, outcome.Result);
        Assert.Equal(target, player.Position);
    }

    [Fact]
    public void MoveAction_Validate_InvalidForNonAdjacentTile()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();
        var farTarget = new Position(1, 1); // not adjacent to (5,5)
        var action = new MoveAction(player.Id, farTarget);

        Assert.Equal(ActionResult.Invalid, action.Validate(world));
    }

    [Fact]
    public void WaitAction_AlwaysSucceeds()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();
        var action = new WaitAction(player.Id);

        Assert.Equal(ActionResult.Success, action.Validate(world));
        var outcome = action.Execute(world);
        Assert.Equal(ActionResult.Success, outcome.Result);
    }

    [Fact]
    public void AttackAction_Validate_SucceedsWhenAdjacent()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        // Player at (5,5), enemy at (7,5) — not adjacent (distance 2)
        // Move enemy adjacent
        enemy.Position = new Position(6, 5);
        world.UpdateEntityPosition(enemy.Id, new Position(7, 5), new Position(6, 5));

        var resolver = new CombatResolver(new Random(0));
        var action = new AttackAction(player.Id, enemy.Id, resolver);

        Assert.Equal(ActionResult.Success, action.Validate(world));
    }

    [Fact]
    public void AttackAction_Validate_InvalidWhenNotAdjacent()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        // Player at (5,5), enemy at (7,5) — ChebyshevTo = 2, not adjacent
        var resolver = new CombatResolver(new Random(0));
        var action = new AttackAction(player.Id, enemy.Id, resolver);

        Assert.Equal(ActionResult.Invalid, action.Validate(world));
    }

    [Fact]
    public void AttackAction_Execute_ProducesCombatEvent()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        enemy.Position = new Position(6, 5);
        world.UpdateEntityPosition(enemy.Id, new Position(7, 5), new Position(6, 5));

        var resolver = new CombatResolver(new Random(0));
        var action = new AttackAction(player.Id, enemy.Id, resolver);

        var outcome = action.Execute(world);

        Assert.Equal(ActionResult.Success, outcome.Result);
        Assert.Single(outcome.CombatEvents);
        Assert.NotEmpty(outcome.LogMessages);
    }
}
