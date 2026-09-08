using System;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class NpcDialogueTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.NpcDialogue filtered numbering and activation agree", FilteredNumbering);
        registry.Add("UI.NpcDialogue paid dressing runs a normal turn with enemy responses", ServiceUsesNormalTurn);
        registry.Add("UI.NpcDialogue unaffordable treatment stays open without a turn", FailedTreatment);
        registry.Add("UI.NpcDialogue Sen report reads state and returns to conversation", ReportAndBack);
        registry.Add("UI.NpcDialogue opaque surface and bounded independent text regions", BoundedLayout);
        registry.Add("UI.NpcDialogue per-NPC greeting rotation resets on bind", GreetingsReset);
    }

    private static void FilteredNumbering()
    {
        var context = CreateContext("quartermaster_vale");
        context.World.Player.Stats.HP = 30;
        context.Dialog.Open(context.Manager.GetInteractionContext()!);
        var markup = context.Dialog.SnapshotBodyMarkup();
        Expect.False(markup.Contains("Dress my wounds", StringComparison.Ordinal), "Full health hides dressing.");
        Expect.True(markup.Contains("2. Help me prepare.", StringComparison.Ordinal), "Filtered choices must be numbered contiguously.");
        context.Dialog.HandleKey(Key.Key2);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("Buy for the trouble", StringComparison.Ordinal), "Number two must activate the displayed second option.");
        context.Dialog.HandleKey(Key.Key1);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("Then leave room", StringComparison.Ordinal), "Missing-item branch should be reachable.");
        Expect.Equal(0, context.World.TurnNumber, "Navigation is read-only.");
    }

    private static void ServiceUsesNormalTurn()
    {
        var context = CreateContext("quartermaster_vale");
        var currencyEvents = 0;
        context.Bus.CurrencyChanged += (_, _) => currencyEvents++;
        context.Dialog.Open(context.Manager.GetInteractionContext()!);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("12g, up to 15 HP, 1 turn", StringComparison.Ordinal), "Treatment must show authored price, cap, and turn cost before activation.");
        context.Dialog.HandleKey(Key.Key2);
        Expect.False(context.Dialog.Visible, "Treatment should leave the conversation before processing enemies.");
        Expect.Equal(25, context.World.Player.Stats.HP, "The Core service should heal.");
        Expect.Equal(28, context.World.Player.GetComponent<WalletComponent>()!.Gold, "The Core service should charge once.");
        Expect.True(context.World.TurnNumber > 0, "Service must advance normal turns.");
        Expect.True(context.Brain.Calls > 0, "Enemies must receive their normal response.");
        Expect.True(currencyEvents > 0, "Normal action flow must publish currency changes.");
    }

    private static void FailedTreatment()
    {
        var context = CreateContext("quartermaster_vale");
        context.World.Player.GetComponent<WalletComponent>()!.Gold = 11;
        context.Dialog.Open(context.Manager.GetInteractionContext()!);
        context.Dialog.HandleKey(Key.Key2);
        Expect.True(context.Dialog.Visible, "Unaffordable treatment should leave the conversation open.");
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("enough gold", StringComparison.Ordinal), "Explain failed treatment inline.");
        Expect.Equal(11, context.World.Player.GetComponent<WalletComponent>()!.Gold, "Failure must not charge.");
        Expect.Equal(10, context.World.Player.Stats.HP, "Failure must not heal.");
        Expect.Equal(0, context.World.TurnNumber, "Failed prevalidation must not advance time.");
        Expect.Equal(0, context.Brain.Calls, "Failed treatment must not run enemies.");
        context.Dialog.HandleKey(Key.Escape);
        Expect.False(context.Dialog.Visible, "Failure must not trap the player in a modal.");
    }

    private static void ReportAndBack()
    {
        var context = CreateContext("field_chronicler");
        var combat = context.World.CombatRandomState;
        var items = context.World.ItemRandomState;
        context.Dialog.Open(context.Manager.GetInteractionContext()!);
        context.Dialog.HandleKey(Key.Key1);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("10/30 HP and 0 health potions", StringComparison.Ordinal), "Report must show current player facts.");
        context.Dialog.HandleKey(Key.Enter);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("Every floor teaches", StringComparison.Ordinal), "Back must restore the same conversation node.");
        Expect.Equal(0, context.World.TurnNumber, "Report and back must not take a turn.");
        Expect.Equal(combat, context.World.CombatRandomState, "Report must preserve combat RNG.");
        Expect.Equal(items, context.World.ItemRandomState, "Report must preserve item RNG.");
    }

    private static void BoundedLayout()
    {
        var context = CreateContext("quartermaster_vale");
        context.Dialog.Open(context.Manager.GetInteractionContext()!);
        var panel = (Panel)context.Dialog.Children.Single(child => child.Name == "Panel");
        var surface = (ColorRect)panel.Children.Single(child => child.Name == "Surface");
        var body = (RichTextLabel)panel.Children.Single(child => child.Name == "BodyLabel");
        var footer = (Label)panel.Children.Single(child => child.Name == "FooterLabel");
        Expect.Equal(1f, surface.Color.A, "The dialog surface must be opaque over the world.");
        Expect.Equal(panel.Size, surface.Size, "Opaque surface must fill the panel.");
        Expect.False(body.FitContent, "Prose must not expand into choices.");
        foreach (var row in panel.Children.OfType<ColorRect>().Where(row => row.Name.ToString().StartsWith("OptionRow", StringComparison.Ordinal) && row.Visible))
        {
            Expect.True(body.Position.Y + body.Size.Y <= row.Position.Y, "Prose must end before choice rows.");
            Expect.True(row.Position.Y + row.Size.Y < footer.Position.Y, "Choice rows must not overlap the footer.");
            Expect.True(row.Position.X + row.Size.X <= panel.Size.X, "Choice rows must fit panel width.");
        }
    }

    private static void GreetingsReset()
    {
        var context = CreateContext("quartermaster_vale");
        var interaction = context.Manager.GetInteractionContext()!;
        context.Dialog.Open(interaction);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("I trade in things", StringComparison.Ordinal), "First opening uses the first greeting.");
        context.Dialog.Close();
        context.Dialog.Open(interaction);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("Fresh bandages", StringComparison.Ordinal), "Same NPC rotates cosmetic greetings.");
        context.Dialog.Open(interaction with { NpcId = EntityId.New() });
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("I trade in things", StringComparison.Ordinal), "Another NPC should have an independent counter.");
        context.Dialog.Bind(context.Manager, context.Bus);
        context.Dialog.Open(interaction);
        Expect.True(context.Dialog.SnapshotBodyMarkup().Contains("I trade in things", StringComparison.Ordinal), "Rebinding resets cosmetic counters.");
    }

    private static Context CreateContext(string npcId)
    {
        var content = ContentLoader.LoadFromRepository();
        var world = new WorldState { Seed = 21 };
        world.InitGrid(6, 6);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity("Patient", new Position(1, 1), new Stats { HP = 10, MaxHP = 30, Speed = 100, ViewRadius = 6 }, Faction.Player);
        player.SetComponent(new WalletComponent { Gold = 40 });
        player.SetComponent(new InventoryComponent());
        world.Player = player;
        world.AddEntity(player);
        var template = content.NpcTemplates[npcId];
        var npc = new Entity(template.DisplayName, new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Speed = 100 }, Faction.Neutral);
        npc.SetComponent(new NpcComponent { TemplateId = npcId, Role = template.Role, DialogueId = template.DialogueId });
        if (template.MerchantOffers is { } offers)
        {
            npc.SetComponent(new MerchantComponent(offers.Select(offer => new MerchantOfferState { ItemTemplateId = offer.ItemTemplateId, Price = offer.Price, Quantity = offer.Quantity })));
        }

        world.AddEntity(npc);
        var brain = new CountingBrain();
        var enemy = new Entity("Observer", new Position(4, 4), new Stats { HP = 10, MaxHP = 10, Speed = 100 }, Faction.Enemy);
        enemy.SetComponent<IBrain>(brain);
        world.AddEntity(enemy);
        var bus = new EventBus();
        var manager = new GameManager();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        manager.LoadWorld(world);
        var dialog = new DialogUI();
        dialog.Bind(manager, bus);
        return new Context(world, manager, bus, dialog, brain);
    }

    private sealed record Context(WorldState World, GameManager Manager, EventBus Bus, DialogUI Dialog, CountingBrain Brain);

    private sealed class CountingBrain : IBrain
    {
        public int Calls { get; private set; }
        public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
        {
            Calls++;
            return new WaitAction(self.Id);
        }
    }
}
