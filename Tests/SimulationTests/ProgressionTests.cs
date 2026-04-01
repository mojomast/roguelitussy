using System;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class ProgressionTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Progression XP threshold calculation is correct for multiple levels", XpThresholdCalculation);
        registry.Add("Simulation.Progression killing enemy grants XP to attacker", KillingEnemyGrantsXp);
        registry.Add("Simulation.Progression level up occurs when XP threshold is met", LevelUpOccursAtThreshold);
        registry.Add("Simulation.Progression multiple level ups from single large XP gain", MultipleLevelUpsFromLargeXp);
        registry.Add("Simulation.Progression stats increase on level up", StatsIncreaseOnLevelUp);
        registry.Add("Simulation.Progression unspent stat points increase by 2 per level", UnspentStatPointsIncrease);
        registry.Add("Simulation.Progression level up grants perk choices", LevelUpGrantsPerkChoice);
        registry.Add("Simulation.Progression perk selection applies stat bonuses", PerkSelectionAppliesBonuses);
        registry.Add("Simulation.Progression kills counter increments on kill", KillsCounterIncrements);
        registry.Add("Simulation.Progression no crash when attacker has no ProgressionComponent", NoCrashWithoutProgression);
        registry.Add("Simulation.Progression no crash when target has no XpValueComponent", NoCrashWithoutXpValue);
    }

    private static void XpThresholdCalculation()
    {
        Expect.Equal(50, ProgressionComponent.CalculateXpThreshold(1), "Level 1 threshold should be 50");
        Expect.Equal(150, ProgressionComponent.CalculateXpThreshold(2), "Level 2 threshold should be 150");
        Expect.Equal(300, ProgressionComponent.CalculateXpThreshold(3), "Level 3 threshold should be 300");
        Expect.Equal(500, ProgressionComponent.CalculateXpThreshold(4), "Level 4 threshold should be 500");
        Expect.Equal(750, ProgressionComponent.CalculateXpThreshold(5), "Level 5 threshold should be 750");
    }

    private static void KillingEnemyGrantsXp()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent());
        var defender = CreateActor("Rat", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 15 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        var progression = attacker.GetComponent<ProgressionComponent>()!;
        Expect.Equal(15, progression.Experience, "Attacker should receive XP from killed enemy");
    }

    private static void LevelUpOccursAtThreshold()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var progression = new ProgressionComponent { Experience = 40 };
        attacker.SetComponent(progression);
        var defender = CreateActor("Enemy", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 10 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        Expect.Equal(2, progression.Level, "Player should level up when XP threshold is reached");
        Expect.Equal(50, progression.Experience, "XP should remain at 50 after gaining 10 from 40");
        Expect.Equal(150, progression.ExperienceToNextLevel, "Next level threshold should be 150 for level 2");
    }

    private static void MultipleLevelUpsFromLargeXp()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent());
        var defender = CreateActor("Boss", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 200 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        var progression = attacker.GetComponent<ProgressionComponent>()!;
        Expect.True(progression.Level >= 3, "Large XP gain should trigger multiple level ups");
        Expect.Equal(200, progression.Experience, "Total XP should reflect full gain");
    }

    private static void StatsIncreaseOnLevelUp()
    {
        var world = CreateWorld(seed: 0);
        var stats = new Stats { HP = 40, MaxHP = 40, Attack = 8, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 };
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, stats);
        attacker.SetComponent(new ProgressionComponent { Experience = 49 });
        var defender = CreateActor("Enemy", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 1 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        Expect.Equal(43, attacker.Stats.MaxHP, "MaxHP should increase by 3 on level up");
        Expect.Equal(9, attacker.Stats.Attack, "Attack should increase by 1 on level up");
    }

    private static void UnspentStatPointsIncrease()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent());
        var defender = CreateActor("Boss", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 200 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        var progression = attacker.GetComponent<ProgressionComponent>()!;
        var expectedPoints = (progression.Level - 1) * 2;
        Expect.Equal(expectedPoints, progression.UnspentStatPoints, "Unspent stat points should be 2 per level gained");
    }

    private static void LevelUpGrantsPerkChoice()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent { Experience = 49 });
        var defender = CreateActor("Rat", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 1 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        var progression = attacker.GetComponent<ProgressionComponent>()!;
        Expect.Equal(1, progression.UnspentPerkChoices, "Each level gained should grant one pending perk choice.");
    }

    private static void PerkSelectionAppliesBonuses()
    {
        var player = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 40, MaxHP = 40, Attack = 8, Defense = 2, Accuracy = 80, Evasion = 10, Speed = 100 });
        player.SetComponent(new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 });
        var content = new StubContentDatabase();

        var selected = ProgressionService.TrySelectPerk(player, content, "battle_instinct", out var message);

        Expect.True(selected, $"Perk selection should succeed. Got: {message}");
        var progression = player.GetComponent<ProgressionComponent>()!;
        Expect.Equal(0, progression.UnspentPerkChoices, "Selecting a perk should consume the pending choice.");
        Expect.True(progression.SelectedPerkIds.Contains("battle_instinct"), "Selected perk ids should record the chosen perk.");
        Expect.Equal(9, player.Stats.Attack, "Battle Instinct should add attack.");
        Expect.Equal(84, player.Stats.Accuracy, "Battle Instinct should add accuracy.");
    }

    private static void KillsCounterIncrements()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent());
        var defender = CreateActor("Rat", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 5 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        new AttackAction(attacker.Id, defender.Id).Execute(world);

        var progression = attacker.GetComponent<ProgressionComponent>()!;
        Expect.Equal(1, progression.Kills, "Kills counter should increment on kill");
    }

    private static void NoCrashWithoutProgression()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var defender = CreateActor("Rat", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        defender.SetComponent(new XpValueComponent { Value = 5 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        var outcome = new AttackAction(attacker.Id, defender.Id).Execute(world);
        Expect.Equal(ActionResult.Success, outcome.Result, "Attack should succeed even without ProgressionComponent on attacker");
    }

    private static void NoCrashWithoutXpValue()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 50, MaxHP = 50, Attack = 100, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        attacker.SetComponent(new ProgressionComponent());
        var defender = CreateActor("Rat", new Position(2, 1), Faction.Enemy, new Stats { HP = 1, MaxHP = 1, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        var outcome = new AttackAction(attacker.Id, defender.Id).Execute(world);
        Expect.Equal(ActionResult.Success, outcome.Result, "Attack should succeed even without XpValueComponent on target");
        Expect.Equal(0, attacker.GetComponent<ProgressionComponent>()!.Experience, "No XP should be awarded without XpValueComponent");
    }

    private static WorldState CreateWorld(int seed = 123)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = seed;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction, Stats? stats = null)
    {
        return new StubEntity(name, position, faction, stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
    }
}
