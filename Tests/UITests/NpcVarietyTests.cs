using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class NpcVarietyTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.NpcVariety public floor population follows authored depth pools", DepthPools);
        registry.Add("UI.NpcVariety repeated seeds reproduce NPC identities and positions", DeterministicPopulation);
        registry.Add("UI.NpcVariety new content validates and uses existing identities and stock", ContentAndIdentity);
        registry.Add("UI.NpcVariety every new dialog node has an unconditional exit", UnconditionalExits);
        registry.Add("UI.NpcVariety services and topics are reachable from each greeting", ReachableGraphs);
        registry.Add("UI.NpcVariety spawned Ilex authorizes her treatment values", IlexTreatment);
        registry.Add("UI.NpcVariety spawned Orin opens stocked merchant interaction", OrinShop);
    }

    private static void DepthPools()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var seed in new[] { 1, 42, 1337 })
        {
            var manager = CreateManager(content, seed);
            foreach (var depth in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 99, 100 })
            {
                Expect.True(manager.TravelToFloor(depth), "Public floor travel should succeed.");
                var expected = depth switch
                {
                    <= 2 => new[] { "field_chronicler", "quartermaster_vale" },
                    <= 4 => new[] { "field_chronicler", "sister_ilex" },
                    <= 6 => new[] { "sister_ilex" },
                    <= 99 => new[] { "cinder_broker_orin" },
                    _ => Array.Empty<string>(),
                };
                var actual = Npcs(manager.World!).Select(entity => entity.GetComponent<NpcComponent>()!.TemplateId).ToArray();
                Expect.True(expected.SequenceEqual(actual), $"Seed {seed}, depth {depth} should have the intended NPC pool, without duplicates.");
                Expect.True(expected.SequenceEqual(content.GetAvailableNpcs(depth).Select(npc => npc.TemplateId)), "Loader eligibility and actual population must agree.");
            }
        }
    }

    private static void DeterministicPopulation()
    {
        var content = ContentLoader.LoadFromRepository();
        var first = CreateManager(content, 7821);
        var second = CreateManager(content, 7821);
        foreach (var depth in new[] { 0, 3, 5, 7, 99, 3 })
        {
            Expect.True(first.TravelToFloor(depth) && second.TravelToFloor(depth), "Identical journeys should succeed.");
            var expected = Npcs(first.World!).Select(entity => (entity.Id, entity.Position, entity.GetComponent<NpcComponent>()!.TemplateId));
            var actual = Npcs(second.World!).Select(entity => (entity.Id, entity.Position, entity.GetComponent<NpcComponent>()!.TemplateId));
            Expect.True(expected.SequenceEqual(actual), $"Seeded NPC population and cached return should agree at depth {depth}.");
        }
    }

    private static void ContentAndIdentity()
    {
        var content = ContentLoader.LoadFromRepository();
        WorldArtCatalog.ClearTextureCachesForTests();
        Expect.True(content.IsValid, "The expanded authored catalog must validate.");
        foreach (var (id, depth, texture) in new[]
        {
            ("sister_ilex", 3, "Knight_Female_Idle_1.png"),
            ("cinder_broker_orin", 7, "Orc_Warrior_Idle_1.png"),
        })
        {
            var manager = CreateManager(content, 91);
            Expect.True(manager.TravelToFloor(depth), "Reach the new NPC's floor through the facade.");
            var npc = Npcs(manager.World!).Single(entity => entity.GetComponent<NpcComponent>()!.TemplateId == id);
            var template = content.NpcTemplates[id];
            var identity = npc.GetComponent<IdentityComponent>();
            Expect.NotNull(identity, "Authored NPC identity must reach the spawned entity.");
            Expect.Equal(template.AppearanceId, identity!.AppearanceId, "Known appearance id must survive projection.");
            var resolved = WorldArtCatalog.GetEntityTexture(npc, content);
            Expect.NotNull(resolved, "Existing catalog sprite must resolve without new assets.");
            Expect.True(resolved!.ResourcePath.EndsWith(texture, StringComparison.Ordinal), "The new identities must use distinct existing portraits.");
        }

        var ilex = content.NpcTemplates["sister_ilex"];
        var orin = content.NpcTemplates["cinder_broker_orin"];
        Expect.False(ilex.IsMerchant, "Ilex should remain a dedicated medic, not another store.");
        Expect.True(orin.IsMerchant, "Orin must provide deep-floor trading.");
        Expect.True(orin.Services?.Any(service => service.Id == "field_dressing" && service.Cost == 22 && service.HealAmount == 30) == true,
            "Orin should provide costly but efficient emergency care when deep-floor recovery becomes scarce.");
        foreach (var itemId in new[] { "potion_health", "item_iron_rations", "scroll_blink", "scroll_phase", "scroll_frost_nova", "armor_plate" })
        {
            Expect.True(orin.MerchantOffers!.Any(offer => offer.ItemTemplateId == itemId), "Orin must cover recovery, escape, control, and protection.");
        }

        foreach (var offer in orin.MerchantOffers!)
        {
            Expect.True(content.TryGetItemTemplate(offer.ItemTemplateId, out _), "Every stock item must already exist in the item catalog.");
            Expect.True(offer.Price > 0 && offer.Quantity > 0, "Stock must have valid finite authored prices and quantities.");
        }
    }

    private static void UnconditionalExits()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var id in new[] { "sister_ilex", "cinder_broker_orin" })
        {
            var dialogue = content.DialogueTemplates[content.NpcTemplates[id].DialogueId];
            foreach (var node in dialogue.Nodes.Values)
            {
                Expect.True(node.Options.Any(option => option.ActionId == "close" && option.Condition is null), $"{id}/{node.NodeId} must always offer an exit.");
                Expect.True(node.Options.All(option => (option.NextNodeId is null) != (option.ActionId is null)), "Every new option must have exactly one outcome.");
            }
        }
    }

    private static void ReachableGraphs()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var (id, depth, requiredAction) in new[]
        {
            ("sister_ilex", 3, "service:field_dressing"),
            ("cinder_broker_orin", 7, "shop"),
        })
        {
            var manager = CreateManager(content, 17);
            Expect.True(manager.TravelToFloor(depth), "New NPC should be reachable through floor travel.");
            var player = manager.World!.Player;
            player.Stats.HP = 1;
            player.SetComponent(new InventoryComponent());
            var dialogue = content.DialogueTemplates[content.NpcTemplates[id].DialogueId];
            foreach (var start in dialogue.StartNodeIds.Prepend(dialogue.StartNodeId).Distinct())
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Queue<string>();
                var actions = new HashSet<string>(StringComparer.Ordinal);
                pending.Enqueue(start);
                while (pending.TryDequeue(out var nodeId))
                {
                    if (!visited.Add(nodeId))
                    {
                        continue;
                    }

                    foreach (var option in DialogueResolver.GetOptions(dialogue.Nodes[nodeId], player))
                    {
                        if (option.NextNodeId is { } next)
                        {
                            pending.Enqueue(next);
                        }

                        if (option.ActionId is { } action)
                        {
                            actions.Add(action);
                        }
                    }
                }

                Expect.True(actions.Contains(requiredAction), $"{id}/{start} must lead to its actual service.");
                var topics = dialogue.Nodes.Keys.Except(dialogue.StartNodeIds);
                Expect.True(topics.All(visited.Contains), $"{id}/{start} should reach every authored topic with injured, unprepared player state.");
            }
        }
    }

    private static void IlexTreatment()
    {
        var content = ContentLoader.LoadFromRepository();
        var manager = CreateManager(content, 52);
        Expect.True(manager.TravelToFloor(3), "Ilex should be present at the crypt approach.");
        var npc = Npcs(manager.World!).Single(entity => entity.GetComponent<NpcComponent>()!.TemplateId == "sister_ilex");
        MoveBeside(manager.World!, npc);
        var interaction = manager.GetInteractionContext() ?? throw new InvalidOperationException("Spawned medic must be interactable.");
        Expect.Equal(npc.Id, interaction.NpcId, "Normal interaction lookup must resolve the spawned medic.");
        var player = manager.World!.Player;
        player.Stats.MaxHP = 100;
        player.Stats.HP = 10;
        player.SetComponent(new WalletComponent { Gold = 100 });
        var service = interaction.NpcTemplate.Services!.Single();
        Expect.Equal(18, service.Cost, "Ilex uses her authored fee, not Vale's.");
        Expect.Equal(25, service.HealAmount, "Ilex uses her authored dressing amount.");
        var dialog = new DialogUI();
        dialog.Bind(manager, null);
        dialog.Open(interaction);
        Expect.True(dialog.SnapshotBodyMarkup().Contains("18g, up to 25 HP, 1 turn", StringComparison.Ordinal), "Spawned medic's dialog must display the actual treatment terms.");
        var action = new NpcServiceAction(player.Id, npc.Id, service.Id);
        Expect.Equal(ActionResult.Success, action.Execute(manager.World!).Result, "Existing Core service must authorize the new medic.");
        Expect.Equal(35, player.Stats.HP, "Ilex's treatment should restore 25 HP.");
        Expect.Equal(82, player.GetComponent<WalletComponent>()!.Gold, "Ilex should charge 18 gold once.");
        player.Stats.HP = player.Stats.MaxHP;
        dialog.Open(interaction);
        Expect.False(dialog.SnapshotBodyMarkup().Contains("18g, up to 25 HP", StringComparison.Ordinal), "Wounds option must disappear at full health.");
    }

    private static void OrinShop()
    {
        var content = ContentLoader.LoadFromRepository();
        var manager = CreateManager(content, 53);
        Expect.True(manager.TravelToFloor(7), "Reach the magma outfitter's first floor.");
        var npc = Npcs(manager.World!).Single(entity => entity.GetComponent<NpcComponent>()!.TemplateId == "cinder_broker_orin");
        MoveBeside(manager.World!, npc);
        var interaction = manager.GetInteractionContext() ?? throw new InvalidOperationException("Spawned outfitter must be interactable.");
        Expect.Equal(npc.Id, interaction.NpcId, "Normal interaction lookup must resolve the spawned outfitter.");
        Expect.True(interaction.IsMerchant, "Spawned stock must enable merchant interaction.");
        var stock = npc.GetComponent<MerchantComponent>()!.Offers;
        Expect.True(interaction.NpcTemplate.MerchantOffers!.Select(offer => (offer.ItemTemplateId, offer.Price, offer.Quantity))
            .SequenceEqual(stock.Select(offer => (offer.ItemTemplateId, offer.Price, offer.Quantity))), "Runtime stock must preserve every authored offer.");
        var requested = EntityId.Invalid;
        var dialog = new DialogUI();
        dialog.Bind(manager, null);
        dialog.ShopRequested += id => requested = id;
        dialog.Open(interaction);
        dialog.HandleKey(Key.Enter);
        Expect.Equal(npc.Id, requested, "First greeting choice should request this NPC's actual shop.");
        Expect.False(dialog.Visible, "Trading should leave the dialog modal.");
    }

    private static IEntity[] Npcs(WorldState world) => world.Entities.Where(entity => entity.GetComponent<NpcComponent>() is not null)
        .OrderBy(entity => entity.GetComponent<NpcComponent>()!.TemplateId, StringComparer.Ordinal).ToArray();

    private static void MoveBeside(WorldState world, IEntity npc)
    {
        var position = Position.Cardinals.Select(delta => npc.Position + delta)
            .First(position => world.IsWalkable(position) && world.GetEntityAt(position) is null
                && Npcs(world).Where(other => other.Id != npc.Id)
                    .All(other => Math.Abs(other.Position.X - position.X) + Math.Abs(other.Position.Y - position.Y) > 1));
        Expect.True(world.MoveEntity(world.Player.Id, position), "Test player should be placed beside only the intended NPC.");
    }

    private static GameManager CreateManager(IContentDatabase content, int seed)
    {
        var manager = new GameManager();
        manager.AttachServices(new WorldState(), new TurnScheduler(), new OpenFloorGenerator(),
            new FOVCalculator(), content, new StubSaveManager(), new EventBus());
        manager.StartNewGame(seed);
        Expect.Equal(GameManager.GameState.Playing, manager.CurrentState, "Content-backed test run should start.");
        return manager;
    }

    private sealed class OpenFloorGenerator : IGenerator
    {
        public LevelData GenerateLevel(WorldState world, int seed, int depth)
        {
            world.InitGrid(16, 16);
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
            var exit = new Position(14, 14);
            world.SetTile(start, TileType.StairsUp);
            world.SetTile(exit, TileType.StairsDown);
            return new LevelData(start, exit, Array.Empty<Position>(), Array.Empty<Position>(), Array.Empty<RoomData>());
        }

        public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => Array.Empty<string>();
    }
}
