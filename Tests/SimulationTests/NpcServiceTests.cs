using System;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class NpcServiceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.NpcService authored dressing heals and charges exactly once", DressingHeals);
        registry.Add("Simulation.NpcService caps healing and rejects repeat full-health use", DressingCapsHealing);
        registry.Add("Simulation.NpcService invalid patients and providers never mutate state", InvalidServicesDoNotCharge);
        registry.Add("Simulation.NpcService execute revalidates changed state", ExecuteRevalidates);
        registry.Add("Simulation.NpcService dialog condition boundaries and fallback", ConditionBoundaries);
        registry.Add("Simulation.NpcService expedition report only reads player facts", ReportIsReadOnly);
    }

    private static void DressingHeals()
    {
        var (world, npc) = CreateWorld();
        var combat = world.CombatRandomState;
        var items = world.ItemRandomState;
        var action = new NpcServiceAction(world.Player.Id, npc.Id, "field_dressing");
        Expect.Equal(ActionResult.Success, action.Validate(world), "Authored provider should treat an injured player.");
        var outcome = action.Execute(world);
        Expect.Equal(ActionResult.Success, outcome.Result, "Treatment should succeed.");
        Expect.Equal(25, world.Player.Stats.HP, "Authored dressing should heal 15 HP.");
        Expect.Equal(28, world.Player.GetComponent<WalletComponent>()!.Gold, "Authored treatment should cost 12 gold.");
        Expect.Equal(1000, action.GetEnergyCost(), "Treatment must cost a normal turn.");
        Expect.True(outcome.LogMessages.Single().Contains("15 HP for 12 gold", StringComparison.Ordinal), "Log actual treatment.");
        Expect.Equal(combat, world.CombatRandomState, "Dressing itself should not roll combat RNG.");
        Expect.Equal(items, world.ItemRandomState, "Dressing should not allocate items.");
    }

    private static void DressingCapsHealing()
    {
        var (world, npc) = CreateWorld();
        world.Player.Stats.HP = 29;
        var action = new NpcServiceAction(world.Player.Id, npc.Id, "field_dressing");
        Expect.Equal(ActionResult.Success, action.Execute(world).Result, "A small wound is eligible.");
        Expect.Equal(30, world.Player.Stats.HP, "Healing must cap at maximum HP.");
        Expect.Equal(ActionResult.Blocked, action.Execute(world).Result, "Full health must reject further payment.");
        Expect.Equal(28, world.Player.GetComponent<WalletComponent>()!.Gold, "A rejected repeat must not charge.");
    }

    private static void InvalidServicesDoNotCharge()
    {
        foreach (var scenario in new[] { "poor", "dead_player", "dead_npc", "hostile", "distant", "diagonal", "unknown_service", "unauthorized", "unknown_template", "missing_component", "missing_npc", "missing_content", "missing_wallet", "non_player" })
        {
            var (world, npc) = CreateWorld();
            var serviceId = "field_dressing";
            switch (scenario)
            {
                case "poor": world.Player.GetComponent<WalletComponent>()!.Gold = 11; break;
                case "dead_player": world.Player.Stats.HP = 0; break;
                case "dead_npc": npc.Stats.HP = 0; break;
                case "hostile":
                    world.RemoveEntity(npc.Id);
                    npc = new Entity("Impostor", new Position(2, 1), new Stats { HP = 1, MaxHP = 1 }, Faction.Enemy);
                    npc.SetComponent(new NpcComponent { TemplateId = "quartermaster_vale" });
                    world.AddEntity(npc);
                    break;
                case "distant": world.MoveEntity(npc.Id, new Position(3, 1)); break;
                case "diagonal": world.MoveEntity(npc.Id, new Position(2, 2)); break;
                case "unknown_service": serviceId = "free_heal"; break;
                case "unauthorized": npc.GetComponent<NpcComponent>()!.TemplateId = "field_chronicler"; break;
                case "unknown_template": npc.GetComponent<NpcComponent>()!.TemplateId = "impostor"; break;
                case "missing_component": npc.RemoveComponent<NpcComponent>(); break;
                case "missing_npc": world.RemoveEntity(npc.Id); break;
                case "missing_content": world.ContentDatabase = null; break;
                case "missing_wallet": world.Player.RemoveComponent<WalletComponent>(); break;
            }

            var hp = world.Player.Stats.HP;
            var gold = world.Player.GetComponent<WalletComponent>()?.Gold ?? 0;
            var combat = world.CombatRandomState;
            var items = world.ItemRandomState;
            var outcome = new NpcServiceAction(scenario == "non_player" ? npc.Id : world.Player.Id, npc.Id, serviceId).Execute(world);
            Expect.True(outcome.Result != ActionResult.Success, $"{scenario} must fail.");
            Expect.Equal(hp, world.Player.Stats.HP, $"{scenario} must not heal.");
            Expect.Equal(gold, world.Player.GetComponent<WalletComponent>()?.Gold ?? 0, $"{scenario} must not charge.");
            Expect.Equal(combat, world.CombatRandomState, $"{scenario} must preserve combat RNG.");
            Expect.Equal(items, world.ItemRandomState, $"{scenario} must preserve item RNG.");
        }
    }

    private static void ExecuteRevalidates()
    {
        var (world, npc) = CreateWorld();
        var action = new NpcServiceAction(world.Player.Id, npc.Id, "field_dressing");
        Expect.Equal(ActionResult.Success, action.Validate(world), "Initial state should validate.");
        world.Player.GetComponent<WalletComponent>()!.Gold = 0;
        Expect.Equal(ActionResult.Blocked, action.Execute(world).Result, "Execute must not trust earlier validation.");
        Expect.Equal(10, world.Player.Stats.HP, "Failed revalidation must not heal.");
    }

    private static void ConditionBoundaries()
    {
        var (world, _) = CreateWorld();
        var injured = new DialogueOption("Treat", null, "service:field_dressing", new DialogueCondition("injured"));
        var missing = new DialogueOption("Supplies", "advice", null, new DialogueCondition("missing_item", "potion_health"));
        var respected = new DialogueOption("Order", "order", null, new DialogueCondition("reputation_at_least", FactionId: "warriors_order", Value: 20));
        Expect.True(DialogueResolver.IsAvailable(injured, world.Player), "Injured option should appear.");
        world.Player.Stats.HP = 30;
        Expect.False(DialogueResolver.IsAvailable(injured, world.Player), "Full-health option should disappear.");
        Expect.True(DialogueResolver.IsAvailable(missing, world.Player), "Missing potion should show preparedness option.");
        world.Player.GetComponent<InventoryComponent>()!.Add(new ItemInstance { TemplateId = "potion_health", StackCount = 1 });
        Expect.False(DialogueResolver.IsAvailable(missing, world.Player), "A carried potion should hide missing-potion advice.");
        var faction = new FactionComponent();
        world.Player.SetComponent(faction);
        faction.Reputation["warriors_order"] = 19;
        Expect.False(DialogueResolver.IsAvailable(respected, world.Player), "Below threshold must not qualify.");
        faction.Reputation["warriors_order"] = 20;
        Expect.True(DialogueResolver.IsAvailable(respected, world.Player), "Exact authored threshold must qualify.");
        Expect.False(DialogueResolver.IsAvailable(respected, null), "Missing context must not unlock conditions.");
        var options = DialogueResolver.GetOptions(new DialogueNode("test", "Test", new[] { injured }), world.Player);
        Expect.Equal("close", options.Single().ActionId!, "An empty filtered graph must have a safe exit.");
    }

    private static void ReportIsReadOnly()
    {
        var (world, _) = CreateWorld();
        world.Depth = 2;
        world.Player.SetComponent(new ProgressionComponent { Level = 3, Kills = 7 });
        var combat = world.CombatRandomState;
        var items = world.ItemRandomState;
        var report = DialogueResolver.BuildExpeditionReport(world);
        Expect.True(report.Contains("depth 2, level 3, 7 kills", StringComparison.Ordinal), "Report should use the current player's record.");
        Expect.True(report.Contains("10/30 HP and 0 health potions", StringComparison.Ordinal), "Report should use carried recovery, not hidden floor loot.");
        Expect.Equal(combat, world.CombatRandomState, "Report must preserve combat RNG.");
        Expect.Equal(items, world.ItemRandomState, "Report must preserve item RNG.");
        Expect.Equal(0, world.TurnNumber, "Report is not a turn.");
        Expect.False(world.IsExplored(new Position(3, 3)), "Report must not reveal the map.");
        Expect.Equal(40, world.Player.GetComponent<WalletComponent>()!.Gold, "Report must not charge.");
    }

    private static (WorldState World, Entity Npc) CreateWorld()
    {
        var world = new WorldState { Seed = 7, ContentDatabase = ContentLoader.LoadFromRepository() };
        world.InitGrid(5, 5);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity("Patient", new Position(1, 1), new Stats { HP = 10, MaxHP = 30, Speed = 100 }, Faction.Player);
        player.SetComponent(new WalletComponent { Gold = 40 });
        player.SetComponent(new InventoryComponent());
        world.Player = player;
        world.AddEntity(player);
        var npc = new Entity("Vale", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Speed = 100 }, Faction.Neutral);
        npc.SetComponent(new NpcComponent { TemplateId = "quartermaster_vale", Role = "shopkeeper", DialogueId = "merchant_intro" });
        world.AddEntity(npc);
        return (world, npc);
    }
}
