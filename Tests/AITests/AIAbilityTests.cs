using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class AIAbilityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Ability enemy uses ranged ability at range", EnemyUsesRangedAbilityAtRange);
        registry.Add("AI.Ability enemy respects ability cooldown", EnemyRespectsAbilityCooldown);
        registry.Add("AI.Ability ambush brain waits then strikes", AmbushBrainWaitsThenStrikes);
        registry.Add("AI.Ability support brain prefers buffing allies", SupportBrainPrefersBuffingAllies);
        registry.Add("AI.Ability melee rusher ignores out-of-range abilities", MeleeRusherIgnoresOutOfRangeAbilities);
        registry.Add("AI.Ability brain factory creates all brain types", BrainFactoryCreatesAllBrainTypes);
    }

    private static void EnemyUsesRangedAbilityAtRange()
    {
        var world = CreateWorldWithContent();
        var pathfinder = new Pathfinder();
        var brain = new RangedKiterBrain();

        var enemy = CreateEnemy(new Position(2, 2));
        var abilitiesComp = new AbilitiesComponent();
        abilitiesComp.Slots.Add(new EnemyAbilitySlot { AbilityId = "arrow_shot", Cooldown = 2, Priority = 80 });
        enemy.SetComponent(abilitiesComp);
        enemy.SetComponent(new CooldownComponent());

        var player = CreatePlayer(new Position(6, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is CastAbilityAction, "Enemy with ranged ability should cast it when target is in range");
        var cast = (CastAbilityAction)action;
        Expect.Equal("arrow_shot", cast.Ability.AbilityId, "Ability should be arrow_shot");
    }

    private static void EnemyRespectsAbilityCooldown()
    {
        var world = CreateWorldWithContent();
        var pathfinder = new Pathfinder();
        var brain = new RangedKiterBrain();

        var enemy = CreateEnemy(new Position(2, 2));
        var abilitiesComp = new AbilitiesComponent();
        abilitiesComp.Slots.Add(new EnemyAbilitySlot { AbilityId = "arrow_shot", Cooldown = 2, Priority = 80 });
        enemy.SetComponent(abilitiesComp);
        var cooldowns = new CooldownComponent();
        cooldowns.SetCooldown("arrow_shot", 2);
        enemy.SetComponent(cooldowns);

        var player = CreatePlayer(new Position(6, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is not CastAbilityAction, "Enemy should not cast ability that is on cooldown");
    }

    private static void AmbushBrainWaitsThenStrikes()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new AmbushBrain();

        var enemy = CreateEnemy(new Position(3, 3));
        var player = CreatePlayer(new Position(8, 8));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var farAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(farAction is WaitAction, "Ambush brain should wait when target is far away");

        var closePlayer = CreatePlayer(new Position(4, 3));
        world.RemoveEntity(player.Id);
        world.Player = closePlayer;
        world.AddEntity(closePlayer);

        var closeAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(closeAction is AttackAction || closeAction is MoveAction, "Ambush brain should act when target is close");
    }

    private static void SupportBrainPrefersBuffingAllies()
    {
        var world = CreateWorldWithContent();
        var pathfinder = new Pathfinder();
        var brain = new SupportBrain();

        var enemy = CreateEnemy(new Position(3, 3));
        var abilitiesComp = new AbilitiesComponent();
        abilitiesComp.Slots.Add(new EnemyAbilitySlot { AbilityId = "war_cry", Cooldown = 3, Priority = 90 });
        enemy.SetComponent(abilitiesComp);
        enemy.SetComponent(new CooldownComponent());

        var ally = new StubEntity("Ally", new Position(4, 3), Faction.Enemy, stats: new Stats { HP = 5, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(7, 3));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        world.AddEntity(ally);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is CastAbilityAction, "Support brain should prefer using buff abilities when allies are nearby");
        var cast = (CastAbilityAction)action;
        Expect.Equal("war_cry", cast.Ability.AbilityId, "Support brain should cast war_cry");
    }

    private static void MeleeRusherIgnoresOutOfRangeAbilities()
    {
        var world = CreateWorldWithContent();
        var pathfinder = new Pathfinder();
        var brain = new MeleeRusherBrain();

        var enemy = CreateEnemy(new Position(2, 2));
        var abilitiesComp = new AbilitiesComponent();
        abilitiesComp.Slots.Add(new EnemyAbilitySlot { AbilityId = "arrow_shot", Cooldown = 2, Priority = 80 });
        enemy.SetComponent(abilitiesComp);
        enemy.SetComponent(new CooldownComponent());

        var player = CreatePlayer(new Position(3, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is AttackAction, "Melee rusher should prefer melee attack over ranged ability when adjacent");
    }

    private static void BrainFactoryCreatesAllBrainTypes()
    {
        Expect.True(BrainFactory.Create("melee_rusher") is MeleeRusherBrain, "Factory should resolve melee_rusher");
        Expect.True(BrainFactory.Create("ranged_kiter") is RangedKiterBrain, "Factory should resolve ranged_kiter");
        Expect.True(BrainFactory.Create("patrol_guard") is PatrolGuardBrain, "Factory should resolve patrol_guard");
        Expect.True(BrainFactory.Create("fleeing") is FleeingBrain, "Factory should resolve fleeing");
        Expect.True(BrainFactory.Create("ambush") is AmbushBrain, "Factory should resolve ambush");
        Expect.True(BrainFactory.Create("support") is SupportBrain, "Factory should resolve support");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(12, 12);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static WorldState CreateWorldWithContent()
    {
        var world = CreateWorld();
        world.ContentDatabase = new StubContentDatabase();
        return world;
    }

    private static StubEntity CreateEnemy(Position position)
    {
        return new StubEntity(
            "Enemy",
            position,
            Faction.Enemy,
            stats: new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
    }

    private static StubEntity CreatePlayer(Position position)
    {
        return new StubEntity(
            "Player",
            position,
            Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
    }
}
