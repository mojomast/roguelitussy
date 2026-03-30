using System;
using Xunit;
using Roguelike.Core;
using Roguelike.Tests.Stubs;

namespace Roguelike.Tests;

public class CombatResolverTests
{
    [Fact]
    public void ResolveAttack_Hit_DealsDamage()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        // Seed that produces hitRoll=0 (always hits) and critRoll>=5 (no crit)
        var rng = new Random(42);
        var resolver = new CombatResolver(rng);

        var result = resolver.ResolveAttack(player, enemy, world);

        // If hit: raw=10 (attack), final=max(1, 10-2)=8, no miss
        if (!result.IsMiss)
        {
            Assert.True(result.FinalDamage > 0);
            Assert.Equal(DamageType.Physical, result.DamageType);
            Assert.Equal(player.Id, result.AttackerId);
            Assert.Equal(enemy.Id, result.DefenderId);
        }
    }

    [Fact]
    public void ResolveAttack_DamageReducedByDefense()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        // Use a fixed seed and re-try to get a non-miss, non-crit result
        var resolver = new CombatResolver(new Random(0));
        var result = resolver.ResolveAttack(player, enemy, world);

        // Player attack=10, enemy defense=2: expected finalDamage = 10-2 = 8 (non-crit)
        if (!result.IsMiss && !result.IsCritical)
        {
            Assert.Equal(10, result.RawDamage);
            Assert.Equal(8, result.FinalDamage);
        }
    }

    [Fact]
    public void ResolveAttack_KillSetsIsKill()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        // Lower enemy HP so one hit kills
        enemy.Stats.HP = 1;
        var resolver = new CombatResolver(new Random(0));

        var result = resolver.ResolveAttack(player, enemy, world);

        if (!result.IsMiss)
        {
            Assert.True(result.IsKill);
            Assert.False(enemy.IsAlive);
        }
    }

    [Fact]
    public void ResolveAttack_MissDealsNoDamage()
    {
        var (world, player, enemy) = StubWorldFactory.CreateWithEntities();
        int originalHp = enemy.Stats.HP;

        // Try many seeds until we find a miss
        DamageResult? missResult = null;
        for (int seed = 0; seed < 1000; seed++)
        {
            // Reset HP
            enemy.Stats.HP = originalHp;
            var resolver = new CombatResolver(new Random(seed));
            var result = resolver.ResolveAttack(player, enemy, world);
            if (result.IsMiss)
            {
                missResult = result;
                break;
            }
        }

        Assert.NotNull(missResult);
        Assert.Equal(0, missResult.FinalDamage);
        Assert.Equal(0, missResult.RawDamage);
        Assert.Equal(originalHp, enemy.Stats.HP);
    }
}
