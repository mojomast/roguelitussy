using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class AuditWiringTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.AuditWiring mouse inventory actions restore keyboard gameplay", MouseActionsRestoreGameplay);
        registry.Add("UI.AuditWiring mouse inventory close restores keyboard gameplay", MouseCloseRestoresGameplay);
        registry.Add("UI.AuditWiring targeted inventory item keeps gameplay gated", TargetedItemKeepsGameplayGated);
        registry.Add("UI.AuditWiring inventory notification lifecycle survives rebind", InventoryNotificationLifecycle);
        registry.Add("UI.AuditWiring restarted run resets turn counter before publishing world", RestartResetsTurnCounter);
        registry.Add("UI.AuditWiring newly generated floor preserves turn counter", FloorTravelPreservesTurnCounter);
    }

    private static void MouseActionsRestoreGameplay()
    {
        foreach (var (templateId, button) in new[]
        {
            ("potion_health", MouseButton.Right),
            ("sword_iron", MouseButton.Right),
            ("potion_health", MouseButton.Middle),
        })
        {
            var context = CreateContext(templateId);
            PressKey(context.Root, Key.I);
            Expect.True(context.Root.Inventory.Visible, "Keyboard input should open inventory.");
            var turnBefore = context.World.TurnNumber;

            context.Root.Inventory.GetChildren().Single(node => node.Name == "Panel").GetChildren().OfType<UiMouseColorRect>().Single(node => node.Name == "Slot0_Background")
                ._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = button });

            Expect.True(context.World.TurnNumber > turnBefore, "The mouse action should execute a gameplay turn.");
            Expect.False(context.Root.Inventory.Visible, "The mouse action should close inventory.");
            AssertKeyboardMovement(context);
        }
    }

    private static void MouseCloseRestoresGameplay()
    {
        var context = CreateContext("potion_health");
        PressKey(context.Root, Key.I);
        var turnBefore = context.World.TurnNumber;

        context.Root.Inventory.GetChildren().Single(node => node.Name == "Panel").GetChildren().OfType<UiMouseLabel>().Single(node => node.Name == "FooterHint_5")
            ._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });

        Expect.False(context.Root.Inventory.Visible, "The close footer should close inventory.");
        Expect.Equal(turnBefore, context.World.TurnNumber, "Closing inventory should not consume a turn.");
        AssertKeyboardMovement(context);
    }

    private static void TargetedItemKeepsGameplayGated()
    {
        var context = CreateContext("scroll_fireball");
        PressKey(context.Root, Key.I);
        var turnBefore = context.World.TurnNumber;
        var positionBefore = context.World.Player.Position;

        context.Root.Inventory.GetChildren().Single(node => node.Name == "Panel").GetChildren().OfType<UiMouseColorRect>().Single(node => node.Name == "Slot0_Background")
            ._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });

        Expect.False(context.Root.Inventory.Visible, "Aimed item use should close inventory.");
        Expect.True(context.Root.TargetingOverlay.IsActive, "Aimed item use should enter targeting.");
        Expect.False(context.Root.InputHandler.HandleKey(Key.Right), "Closing inventory must not enable gameplay during targeting.");
        PressKey(context.Root, Key.Right);
        Expect.Equal(positionBefore + new Position(1, 0), context.Root.TargetingOverlay.CursorPosition, "Root keyboard input should move only the targeting cursor.");
        Expect.Equal(positionBefore, context.World.Player.Position, "Targeting must not move the player.");
        Expect.Equal(turnBefore, context.World.TurnNumber, "Entering and moving targeting should not consume a turn.");

        PressKey(context.Root, Key.Escape);
        Expect.False(context.Root.TargetingOverlay.IsActive, "Escape should cancel targeting.");
        Expect.Equal(1, context.World.Player.GetComponent<InventoryComponent>()!.Items.Count, "Cancelling targeting should retain the scroll.");
        AssertKeyboardMovement(context);
    }

    private static void InventoryNotificationLifecycle()
    {
        var context = CreateContext("potion_health");
        context.Root.BindServices(context.Manager, context.Bus, context.Content);
        var changes = 0;
        context.Root.Inventory.OpenStateChanged += () => changes++;
        context.Root.Inventory.Open();
        context.Root.Inventory.Open();
        Expect.False(context.Root.InputHandler.HandleKey(Key.Right), "Inventory opened outside keyboard routing should gate gameplay after rebind.");
        context.Root.Inventory.Close();
        context.Root.Inventory.Close();
        Expect.Equal(2, changes, "Only actual open/close transitions should notify listeners.");
        AssertKeyboardMovement(context);

        context.Root._ExitTree();
        context.Root.Inventory.Open();
        Expect.True(context.Root.InputHandler.HandleKey(Key.Right), "An exited root should no longer react to inventory notifications.");
        context.Root.BindServices(context.Manager, context.Bus, context.Content);
        context.Root.Inventory.Close();
        AssertKeyboardMovement(context);
    }

    private static void RestartResetsTurnCounter()
    {
        var context = CreateContext("potion_health");
        context.Manager.StartNewGame(42);
        var previousWorld = context.Manager.World!;
        PressKey(context.Root, Key.Space);
        PressKey(context.Root, Key.Space);
        var previousTurns = previousWorld.TurnNumber;
        Expect.True(previousTurns > 0, "The previous session should have taken turns.");
        var publishedTurns = -1;
        context.Bus.FloorChanged += _ => publishedTurns = context.Manager.World!.TurnNumber;

        context.Manager.StartNewGame(previousWorld.Seed);

        Expect.Equal(GameManager.GameState.Playing, context.Manager.CurrentState, "Restart should successfully start a run.");
        Expect.False(ReferenceEquals(previousWorld, context.Manager.World), "Restart should replace the old world.");
        Expect.Equal(0, context.Manager.World!.TurnNumber, "A restarted run should begin at turn zero.");
        Expect.Equal(0, publishedTurns, "Presentation must receive the reset counter, not the previous session's counter.");
        Expect.Equal(0, context.Manager.CurrentRunStats.TotalTurns, "New run statistics should start at zero.");
        Expect.Equal(0, context.Manager.CurrentFloorStats.TurnsSpent, "The new floor turn baseline should start at zero.");
        Expect.Equal(previousTurns, previousWorld.TurnNumber, "Resetting the new run must not mutate the previous world.");
    }

    private static void FloorTravelPreservesTurnCounter()
    {
        var context = CreateContext("potion_health");
        PressKey(context.Root, Key.Space);
        PressKey(context.Root, Key.Space);
        var turnBefore = context.World.TurnNumber;
        Expect.True(turnBefore > 0, "The source floor should have a nonzero turn counter.");

        Expect.True(context.Manager.TravelToFloor(1), "Travel to a newly generated floor should succeed.");

        Expect.Equal(1, context.Manager.CurrentFloor, "Travel should load the destination floor.");
        Expect.Equal(turnBefore, context.Manager.World!.TurnNumber, "Floor generation during travel must preserve the run counter.");
        Expect.Equal(0, context.Manager.CurrentFloorStats.TurnsSpent, "The destination's floor-local counter should start at zero.");
    }

    private static void AssertKeyboardMovement(Context context)
    {
        var positionBefore = context.World.Player.Position;
        var turnBefore = context.World.TurnNumber;
        PressKey(context.Root, Key.Right);
        Expect.Equal(positionBefore + new Position(1, 0), context.World.Player.Position, "Keyboard gameplay should resume after inventory closes.");
        Expect.True(context.World.TurnNumber > turnBefore, "Resumed keyboard movement should execute a turn.");
    }

    private static void PressKey(UIRoot root, Key key) =>
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = key });

    private static Context CreateContext(string templateId)
    {
        var world = new WorldState();
        var generator = new StubGenerator();
        generator.GenerateLevel(world, 42, 0);
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 40, Speed = 100, ViewRadius = 8 });
        var inventory = new InventoryComponent(20);
        inventory.Add(new ItemInstance { TemplateId = templateId, IsIdentified = true });
        player.SetComponent(inventory);
        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var content = new StubContentDatabase();
        var manager = new GameManager();
        manager.AttachServices(world, new TurnScheduler(), generator, new FOVCalculator(), content, new StubSaveManager(), bus);
        manager.LoadWorld(world);
        var root = new UIRoot();
        root.BindServices(manager, bus, content);
        return new Context(world, manager, bus, content, root);
    }

    private sealed record Context(WorldState World, GameManager Manager, EventBus Bus, StubContentDatabase Content, UIRoot Root);
}
