using Xunit;
using Roguelike.Core;
using Roguelike.Tests.Stubs;

namespace Roguelike.Tests;

public class TurnSchedulerTests
{
    [Fact]
    public void BeginRound_AccumulatesEnergy()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        var scheduler = new TurnScheduler();
        scheduler.Register(player);
        scheduler.Register(enemy);

        scheduler.BeginRound(world);

        // Player speed=100 -> energy += 100*10 = 1000
        Assert.Equal(1000, player.Stats.Energy);
        // Enemy speed=80 -> energy += 80*10 = 800
        Assert.Equal(800, enemy.Stats.Energy);
    }

    [Fact]
    public void GetNextActor_ReturnsHighestEnergyFirst()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        var scheduler = new TurnScheduler();
        scheduler.Register(player);
        scheduler.Register(enemy);

        scheduler.BeginRound(world);

        // Only player has >= 1000 energy threshold
        Assert.True(scheduler.HasNextActor());
        var actor = scheduler.GetNextActor();
        Assert.Same(player, actor);
    }

    [Fact]
    public void SlowerEntity_NeedsMultipleRounds()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        var scheduler = new TurnScheduler();
        scheduler.Register(player);
        scheduler.Register(enemy);

        // Round 1: enemy gets 800 energy, not enough
        scheduler.BeginRound(world);
        Assert.True(scheduler.HasNextActor());
        var actor1 = scheduler.GetNextActor();
        Assert.Same(player, actor1);
        scheduler.ConsumeEnergy(player.Id, 1000);
        Assert.False(scheduler.HasNextActor());
        scheduler.EndRound(world);

        // Round 2: enemy gets 800 more = 1600, now ready
        scheduler.BeginRound(world);
        Assert.True(scheduler.HasNextActor());
        var first = scheduler.GetNextActor();
        // Enemy has 1600 energy, player has 1000 — enemy goes first
        Assert.Same(enemy, first);
    }

    [Fact]
    public void Unregister_RemovesEntityFromScheduler()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        var scheduler = new TurnScheduler();
        scheduler.Register(player);
        scheduler.Register(enemy);

        scheduler.Unregister(enemy.Id);
        scheduler.BeginRound(world);

        // Only player should act
        var actor = scheduler.GetNextActor();
        Assert.Same(player, actor);
        Assert.Equal(0, enemy.Stats.Energy); // enemy NOT granted energy
    }

    [Fact]
    public void EndRound_IncrementsTurnNumber()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var scheduler = new TurnScheduler();
        Assert.Equal(1, world.TurnNumber);

        scheduler.EndRound(world);
        Assert.Equal(2, world.TurnNumber);
    }
}
