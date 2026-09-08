using System;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class RaceAbilityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Race definitions normalize and map heritage techniques", RaceDefinitionsNormalizeAndMapTechniques);
        registry.Add("Simulation.Race heritage reconciliation preserves class slots and cooldowns", HeritageReconciliationPreservesExistingSlots);
    }

    private static void RaceDefinitionsNormalizeAndMapTechniques()
    {
        Expect.Equal("human", RaceDefinitions.NormalizeId(null), "Blank races should normalize to human.");
        Expect.Equal("human", RaceDefinitions.NormalizeId("unknown"), "Unknown races should normalize to human.");
        Expect.Equal("phase_shift", RaceDefinitions.Get("elf").HeritageAbilityId, "Elf heritage should be Phase Shift.");
        Expect.Equal("ground_slam", RaceDefinitions.Get("dwarf").HeritageAbilityId, "Dwarf heritage should be Ground Slam.");
        Expect.Equal("heavy_slam", RaceDefinitions.Get("orc").HeritageAbilityId, "Orc heritage should be Heavy Slam.");
    }

    private static void HeritageReconciliationPreservesExistingSlots()
    {
        var world = CreateWorld();
        var player = world.Player;
        player.SetComponent(new IdentityComponent { RaceId = "elf" });
        var abilities = new AbilitiesComponent();
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "arcane_bolt", Cooldown = 0, Priority = 100 });
        player.SetComponent(abilities);
        var cooldowns = new CooldownComponent();
        cooldowns.SetCooldown("arcane_bolt", 3);
        player.SetComponent(cooldowns);

        var manager = new GameManager();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager());
        manager.LoadWorld(world);
        manager.LoadWorld(world);

        Expect.Equal("arcane_bolt", abilities.Slots[0].AbilityId, "Existing class slot order must remain first.");
        Expect.Equal("phase_shift", abilities.Slots[1].AbilityId, "Heritage slot should append after class slots.");
        Expect.Equal(2, abilities.Slots.Count, "Repeated world loads must not duplicate heritage slots.");
        Expect.Equal(3, cooldowns.GetCooldown("arcane_bolt"), "Existing cooldown state must be preserved.");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(4, 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity("Player", new Position(1, 1), new Stats { HP = 20, MaxHP = 20, Speed = 100 }, Faction.Player);
        world.Player = player;
        world.AddEntity(player);
        return world;
    }
}
