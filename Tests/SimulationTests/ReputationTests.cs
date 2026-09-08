using System.Reflection;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;
using Godotussy;

namespace Roguelike.Tests.SimulationTests;

public sealed class ReputationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Reputation merchant discount uses authored friendly threshold", MerchantDiscountUsesFriendlyThreshold);
        registry.Add("Simulation.Reputation hostile merchants do not mutate player stats", HostileMerchantsDoNotMutateStats);
        registry.Add("Simulation.Reputation merchant discount composes with perk and ascension prices", MerchantDiscountComposesWithOtherPriceRules);
    }

    private static void MerchantDiscountUsesFriendlyThreshold()
    {
        var content = ContentLoader.LoadFromRepository();
        var player = CreatePlayer();
        var faction = player.GetComponent<FactionComponent>()!;

        faction.Reputation["merchants_guild"] = 29;
        Expect.Equal(0, ReputationService.ResolveMerchantDiscountPercent(player, content), "Reputation below the authored threshold should not discount prices");

        faction.Reputation["merchants_guild"] = 30;
        Expect.Equal(10, ReputationService.ResolveMerchantDiscountPercent(player, content), "Exact authored friendly reputation should grant the guild discount");
    }

    private static void HostileMerchantsDoNotMutateStats()
    {
        var content = ContentLoader.LoadFromRepository();
        var player = CreatePlayer();
        player.GetComponent<FactionComponent>()!.Reputation["merchants_guild"] = -30;
        var statsBefore = (player.Stats.HP, player.Stats.MaxHP, player.Stats.Attack, player.Stats.Defense, player.Stats.Evasion);

        Expect.Equal(0, ReputationService.ResolveMerchantDiscountPercent(player, content), "Hostile merchants should not grant a discount");
        ReputationService.ApplyPassiveStats(player);

        Expect.Equal(statsBefore, (player.Stats.HP, player.Stats.MaxHP, player.Stats.Attack, player.Stats.Defense, player.Stats.Evasion), "Merchant reputation must not mutate player stats");
    }

    private static void MerchantDiscountComposesWithOtherPriceRules()
    {
        var content = ContentLoader.LoadFromRepository();
        var player = CreatePlayer();
        player.GetComponent<FactionComponent>()!.Reputation["merchants_guild"] = 30;
        var progression = new ProgressionComponent();
        progression.SelectedPerkIds.Add("quartermasters_eye");
        player.SetComponent(progression);

        var manager = new GameManager();
        var world = CreateWorld(player, content);
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager());
        typeof(GameManager).GetField("_runAscensionLevel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, 2);
        Expect.Equal(84, manager.ResolveMerchantBuyPrice(100), "Friendly reputation and the 20% perk should apply before the authored 20% ascension tariff");
    }

    private static Entity CreatePlayer()
    {
        var player = new Entity("Player", new Position(1, 1), new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 3, Evasion = 2, Speed = 100 }, Faction.Player);
        player.SetComponent(new FactionComponent());
        return player;
    }

    private static WorldState CreateWorld(Entity player, IContentDatabase content)
    {
        var world = new WorldState { Seed = 1234, Depth = 1, ContentDatabase = content };
        world.InitGrid(4, 4);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.Player = player;
        world.AddEntity(player);
        return world;
    }
}
