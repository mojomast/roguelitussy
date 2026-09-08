using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class ShrinePresentationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Shrine renders altar body rune and label", RendersShrine);
        registry.Add("UI.Shrine resolves stat perk and guarded relic rewards", ResolvesRewards);
        registry.Add("UI.Shrine lethal enemy response clears pending reward", LethalResponseClearsReward);
    }

    private static void RendersShrine()
    {
        var shrine = new StubEntity("Shrine", new Position(1, 1), Faction.Neutral);
        shrine.SetComponent(new ShrineComponent { ShrineType = "relic", HPCost = 15 });
        var renderer = new EntityRenderer();
        renderer.UpsertEntity(shrine);
        var children = renderer.GetSprite(shrine.Id)!.GetChildren();

        Expect.True(children.Single(child => child.Name == "Body") is ColorRect, "Shrines must use a compact procedural altar body.");
        Expect.True(children.Any(child => child.Name == "ShrineRune"), "Shrines must render a rune accent.");
        Expect.True(children.Any(child => child.Name == "ShrineLabel"), "Shrines must render a visible reward label.");
    }

    private static void ResolvesRewards()
    {
        var content = ContentLoader.LoadFromRepository();
        var save = new StubSaveManager();
        var bus = new EventBus();
        var manager = CreateManager(content, save, bus);
        manager.StartNewGame(1312);
        var player = manager.World!.Player;
        var shrine = manager.World.Entities.Single(entity => entity.GetComponent<ShrineComponent>() is not null);
        Expect.True(manager.TeleportPlayer(new Position(2, 1)), "Test setup must place the player beside the shrine.");

        var hpBefore = player.Stats.HP;
        var statsBefore = player.GetComponent<ProgressionComponent>()!.UnspentStatPoints;
        manager.ProcessPlayerAction(new InteractShrineAction(player.Id, shrine.Id));
        Expect.Equal(hpBefore - 8, player.Stats.HP, "Shrine interaction must retain its authored HP cost.");
        Expect.Equal(statsBefore + 1, player.GetComponent<ProgressionComponent>()!.UnspentStatPoints, "Stat shrines grant exactly one point.");
        Expect.False(shrine.GetComponent<ShrineComponent>()!.RewardChoicePending, "Immediate shrine rewards must clear their pending state.");

        var perkShrine = AddShrine(manager.World, new Position(2, 2), "perk", 1);
        var perksBefore = player.GetComponent<ProgressionComponent>()!.UnspentPerkChoices;
        manager.ProcessPlayerAction(new InteractShrineAction(player.Id, perkShrine.Id));
        Expect.Equal(perksBefore + 1, player.GetComponent<ProgressionComponent>()!.UnspentPerkChoices, "Perk shrines grant exactly one choice.");
        Expect.False(perkShrine.GetComponent<ShrineComponent>()!.RewardChoicePending, "Perk rewards must clear pending state.");

        var relicShrine = AddShrine(manager.World, new Position(3, 2), "relic", 1);
        var overlayCount = 0;
        IReadOnlyList<RelicTemplate>? offers = null;
        bus.RelicChoiceReady += choices => { overlayCount++; offers = choices; };
        manager.ProcessPlayerAction(new InteractShrineAction(player.Id, relicShrine.Id));
        Expect.Equal(1, overlayCount, "A successful relic shrine interaction must emit one overlay event.");
        Expect.Equal(3, offers!.Count, "Relic shrines must offer exactly three choices when the pool permits.");
        Expect.False(manager.ProcessRelicChoice("not_offered", out _), "External relic choices outside the offer list must be rejected.");
        Expect.True(relicShrine.GetComponent<ShrineComponent>()!.RewardChoicePending, "Invalid relic claims must preserve pending state.");

        Expect.True(manager.SaveToSlot(2), "Pending relic reward should save through the facade.");
        var loadedBus = new EventBus();
        var loaded = CreateManager(content, save, loadedBus);
        IReadOnlyList<RelicTemplate>? restoredOffers = null;
        var relicChanged = 0;
        loadedBus.RelicChoiceReady += choices => restoredOffers = choices;
        loadedBus.RelicsChanged += (_, _) => relicChanged++;
        Expect.True(loaded.LoadFromSlot(2), "Saved pending relic reward should load.");
        var offeredOnLoad = restoredOffers ?? throw new InvalidOperationException("Reload should re-emit pending relic offers.");
        Expect.True(offers.Select(choice => choice.RelicId).SequenceEqual(offeredOnLoad.Select(choice => choice.RelicId)),
            "Reload must re-emit the same deterministic shrine offers.");
        var selectedRelicId = offeredOnLoad[0].RelicId;
        Expect.True(loaded.ProcessRelicChoice(selectedRelicId), "An offered relic should claim successfully.");
        Expect.False(loaded.ProcessRelicChoice(selectedRelicId), "A claimed shrine reward must not be claimed twice.");
        Expect.Equal(1, relicChanged, "A valid relic claim must refresh the relic presentation once.");
    }

    private static Entity AddShrine(WorldState world, Position position, string type, int hpCost)
    {
        var shrine = new Entity("Shrine", position, new Stats { HP = 1, MaxHP = 1 }, Faction.Neutral, blocksMovement: false, blocksSight: false);
        shrine.SetComponent(new ShrineComponent { ShrineType = type, HPCost = hpCost });
        world.AddEntity(shrine);
        return shrine;
    }

    private static void LethalResponseClearsReward()
    {
        var content = ContentLoader.LoadFromRepository();
        var bus = new EventBus();
        var manager = CreateManager(content, new StubSaveManager(), bus);
        manager.StartNewGame(137);
        var world = manager.World!;
        var player = world.Player;
        player.Stats.HP = 20;
        player.Stats.MaxHP = 20;
        var shrine = AddShrine(world, new Position(3, 2), "relic", 19);
        var enemy = new StubEntity("Executioner", new Position(2, 2), Faction.Enemy,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 20, Accuracy = 100, Defense = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        enemy.SetComponent<IBrain>(new MeleeRusherBrain());
        world.AddEntity(enemy);
        manager.LoadWorld(world);
        Expect.True(manager.TeleportPlayer(new Position(2, 1)), "Test setup must place the player beside shrine and executioner.");
        var offers = 0;
        bus.RelicChoiceReady += _ => offers++;

        manager.ProcessPlayerAction(new InteractShrineAction(player.Id, shrine.Id));

        Expect.False(player.IsAlive, "The enemy response should kill the near-empty petitioner.");
        Expect.False(shrine.GetComponent<ShrineComponent>()!.RewardChoicePending, "A dead petitioner must not leave a claimable shrine reward.");
        Expect.Equal(0, offers, "Lethal shrine turns must not open relic selection.");
        Expect.False(manager.ProcessRelicChoice("vampire_fang"), "A dead player cannot claim relics directly.");
    }

    private static GameManager CreateManager(IContentDatabase content, StubSaveManager save, EventBus bus)
    {
        var manager = new GameManager();
        manager.AttachServices(new WorldState(), new TurnScheduler(), new ShrineGenerator(), new FOVCalculator(), content, save, bus);
        return manager;
    }

    private sealed class ShrineGenerator : IGenerator
    {
        public LevelData GenerateLevel(WorldState world, int seed, int depth)
        {
            world.InitGrid(8, 8);
            world.Seed = seed;
            world.Depth = depth;
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetTile(new Position(x, y), TileType.Floor);
                }
            }

            var start = new Position(1, 1);
            var exit = new Position(6, 6);
            world.SetTile(start, TileType.StairsUp);
            world.SetTile(exit, TileType.StairsDown);
            return new LevelData(start, exit, Array.Empty<Position>(), Array.Empty<Position>(), Array.Empty<RoomData>(),
                ShrineSpawns: new[] { new ShrineSpawnData(new Position(3, 1), "shrine_stat") });
        }

        public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => Array.Empty<string>();
    }
}
