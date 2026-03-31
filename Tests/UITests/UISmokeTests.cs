using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class UISmokeTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.GameManager new game generates a populated floor", GameManagerGeneratesPopulatedFloor);
        registry.Add("UI.GameManager floor travel preserves run state", GameManagerPreservesRunStateAcrossFloors);
        registry.Add("UI.HUD updates from bus and exposes keyboard toggles", HudUpdatesFromBus);
        registry.Add("UI.Minimap reflects explored tiles and gameplay toggles", MinimapReflectsExplorationAndToggles);
        registry.Add("UI.MainMenu character creation affects the starting run", MainMenuCharacterCreationAffectsStartingRun);
        registry.Add("UI.Inventory keyboard navigation emits concrete actions", InventoryEmitsConcreteActions);
        registry.Add("UI.Help overlay opens from menu and gameplay", HelpOverlayOpensFromMenuAndGameplay);
        registry.Add("UI.CombatLog escapes BBCode and reacts to combat events", CombatLogReactsToEvents);
        registry.Add("UI.Overlays clamp to the viewport", OverlaysClampToViewport);
        registry.Add("UI.UIRoot routes keyboard between title, overlays, and gameplay", UIRootRoutesKeyboard);
    }

    private static void GameManagerGeneratesPopulatedFloor()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        gameManager.StartNewGame(1337);

        Expect.NotNull(gameManager.World, "Starting a new game should create a world.");
        Expect.Equal(GameManager.GameState.Playing, gameManager.CurrentState, "Starting a new game should enter the playing state.");
        Expect.NotNull(gameManager.World!.Player, "The generated world should include a player.");
        Expect.True(gameManager.World.Entities.Count >= 2, "The generated world should include at least one enemy alongside the player.");
        Expect.True(gameManager.World.GetItemsAt(new Position(4, 4)).Count > 0, "Item spawns should be populated on the generated floor.");
        Expect.True(CountVisibleTiles(gameManager.World) > 0, "Starting a new game should calculate visible tiles for the active floor.");
    }

    private static void GameManagerPreservesRunStateAcrossFloors()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);
        gameManager.StartNewGame(7331);

        var player = gameManager.World!.Player;
        player.Stats.HP = 27;

        EntityId enemyId = EntityId.Invalid;
        foreach (var entity in gameManager.World.Entities)
        {
            if (entity.Id != player.Id)
            {
                enemyId = entity.Id;
                break;
            }
        }

        Expect.True(enemyId.IsValid, "The starting floor should contain an enemy to mutate before traveling.");
        gameManager.World.RemoveEntity(enemyId);
        gameManager.World.MoveEntity(player.Id, new Position(8, 8));

        var descendOutcome = gameManager.ProcessPlayerAction(new DescendAction(player.Id));
        Expect.Equal(ActionResult.Success, descendOutcome.Result, "Descending should succeed from the stairs-down tile.");
        Expect.Equal(1, gameManager.World!.Depth, "Descending should move the player to the next floor.");
        Expect.Equal(27, gameManager.World.Player.Stats.HP, "Player HP should carry over when traveling downward.");

        var descendedPlayerId = gameManager.World.Player.Id;
        gameManager.World.MoveEntity(descendedPlayerId, new Position(1, 1));

        var ascendOutcome = gameManager.ProcessPlayerAction(new AscendAction(descendedPlayerId));
        Expect.Equal(ActionResult.Success, ascendOutcome.Result, "Ascending should succeed from the stairs-up tile.");
        Expect.Equal(0, gameManager.World!.Depth, "Ascending should return to the original floor.");
        Expect.Equal(27, gameManager.World.Player.Stats.HP, "Player HP should still match the carried run state after returning.");
        Expect.True(gameManager.World.GetEntity(enemyId) is null, "Floor-local mutations should persist when revisiting a cached floor.");
    }

    private static void HudUpdatesFromBus()
    {
        var context = CreateContext();
        var hud = new HUD();
        hud.Bind(context.GameManager, context.Bus);

        context.World.Depth = 3;
        context.World.TurnNumber = 17;
        context.Player.Stats.HP = 25;
        context.Player.Stats.MaxHP = 100;

        context.Bus.EmitFloorChanged(3);
        context.Bus.EmitHPChanged(context.Player.Id, 25, 100);
        context.Bus.EmitTurnCompleted();

        Expect.Equal("HP: 25/100", hud.HPText, "HUD should render player HP from bus events");
        Expect.Equal("Floor: 3", hud.FloorText, "HUD should render the current floor");
        Expect.Equal("Turn: 17", hud.TurnText, "HUD should render the current turn number");
        Expect.Equal(Colors.Red.R, hud.HPColor.R, "Low HP should switch the HUD into the red threshold");

        var before = hud.MinimapText;
        hud.ToggleMinimap();
        Expect.False(before == hud.MinimapText, "Toggling the minimap should change the HUD summary");
    }

    private static void MinimapReflectsExplorationAndToggles()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);

        var minimap = new Minimap();
        minimap.Bind(context.GameManager, context.Bus);

        Expect.True(minimap.Visible, "The minimap should be visible during gameplay by default.");
        Expect.True(minimap.VisibleTileCount > 0, "The minimap should reflect currently visible tiles.");
        Expect.True(minimap.ExploredTileCount >= minimap.VisibleTileCount, "Explored tiles should include the current visible region.");
        Expect.True(minimap.PlayerWorldPosition != Position.Invalid, "The minimap should track the player marker.");

        minimap.Toggle();
        Expect.False(minimap.Visible, "Toggling the minimap should hide the overlay.");

        minimap.Toggle();
        Expect.True(minimap.Visible, "Toggling the minimap again should restore the overlay.");
    }

    private static void InventoryEmitsConcreteActions()
    {
        var useContext = CreateContext(new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true });
        var tooltip = new Tooltip();
        var inventory = new InventoryUI();
        inventory.Bind(useContext.GameManager, useContext.Bus, useContext.Content, tooltip);

        IAction? submitted = null;
        useContext.Bus.PlayerActionSubmitted += action => submitted = action;

        inventory.Open();
        Expect.Equal(0, inventory.SelectedIndex, "Inventory should start on the first slot");
        Expect.True(inventory.GridText.Contains("Sort: Equipped"), "Inventory should expose the active sort mode in the overlay text");
        Expect.True(inventory.DescriptionText.Contains("Category:"), "Inventory should provide item-management details for the selected slot");
        inventory.HandleKey(Key.Tab);
        Expect.True(inventory.GridText.Contains("Sort: Category"), "Tab should cycle inventory sorting modes");
        inventory.HandleKey(Key.Right);
        Expect.Equal(1, inventory.SelectedIndex, "Right arrow should move selection across the grid");
        inventory.HandleKey(Key.Left);
        inventory.HandleKey(Key.Enter);

        Expect.True(submitted is UseItemAction, "Using an inventory item should emit a concrete UseItemAction");

    var equipContext = CreateContext(new ItemInstance { TemplateId = "sword_iron", IsIdentified = true });
    var equipInventory = new InventoryUI();
    equipInventory.Bind(equipContext.GameManager, equipContext.Bus, equipContext.Content, tooltip);
    IAction? equipped = null;
    equipContext.Bus.PlayerActionSubmitted += action => equipped = action;

    equipInventory.Open();
    equipInventory.HandleKey(Key.E);

    Expect.True(equipped is ToggleEquipAction, "Equipping an inventory item should emit a concrete ToggleEquipAction");

    var dropContext = CreateContext(new ItemInstance { TemplateId = "potion_health", StackCount = 3, IsIdentified = true });
        var dropInventory = new InventoryUI();
        dropInventory.Bind(dropContext.GameManager, dropContext.Bus, dropContext.Content, tooltip);
        IAction? dropped = null;
        dropContext.Bus.PlayerActionSubmitted += action => dropped = action;

        dropInventory.Open();
        dropInventory.HandleKey(Key.D);

        Expect.True(dropped is DropItemAction, "Dropping an inventory item should emit a concrete DropItemAction");
        Expect.Equal(1, (dropped as DropItemAction)?.Quantity ?? 0, "Dropping a stack from the UI should default to a one-item split-drop.");
    }

    private static void MainMenuCharacterCreationAffectsStartingRun()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        var content = new StubContentDatabase();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Up);
        menu.HandleKey(Key.Enter);

        var player = gameManager.World!.Player;
        var inventory = player.GetComponent<InventoryComponent>();

        Expect.Equal("Iris", player.Name, "Character creation should apply the selected preset name to the starting run.");
        Expect.Equal("Skirmisher", gameManager.CharacterOptions.Archetype, "Character creation should persist the selected archetype.");
        Expect.Equal("Scout", gameManager.CharacterOptions.Origin, "Character creation should persist the selected origin.");
        Expect.Equal("Quartermaster", gameManager.CharacterOptions.Trait, "Character creation should persist the selected trait.");
        Expect.True(player.Stats.Speed > 100, "Archetype and origin bonuses should affect player stats.");
        Expect.True(player.Stats.Accuracy > 80, "Allocated finesse points should affect player accuracy.");
        Expect.True(inventory?.Capacity > 20, "Trait bonuses should be able to expand inventory capacity.");
        Expect.True(inventory?.GetEquipped(EquipSlot.MainHand) is not null, "A loadout archetype should start with equipment already equipped.");
    }

    private static void HelpOverlayOpensFromMenuAndGameplay()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.H });
        Expect.True(root.HelpOverlay.Visible, "Help should open from the title flow.");
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.HelpOverlay.Visible, "Escape should close the help overlay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.H });
        Expect.True(root.HelpOverlay.Visible, "Help should also open during gameplay.");
    }

    private static void CombatLogReactsToEvents()
    {
        var context = CreateContext();
        var enemy = new StubEntity("Skeleton", new Position(3, 1), Faction.Enemy);
        context.World.AddEntity(enemy);

        var log = new CombatLog();
        log.Bind(context.GameManager, context.Bus);

        context.Bus.EmitLogMessage("A [trap] snaps.");
        context.Bus.EmitDamageDealt(new DamageResult(context.Player.Id, enemy.Id, 5, 4, DamageType.Physical, false, false, false));

        Expect.True(log.RenderedText.Contains("[lb]trap[rb]"), "Combat log should escape BBCode brackets");
        Expect.True(log.RenderedText.Contains("hits Skeleton for 4 damage"), "Combat log should append derived combat messages");
    }

    private static void OverlaysClampToViewport()
    {
        WithViewportSize(new Vector2(640f, 360f), viewportSize =>
        {
            var context = CreateContext();
            var root = new Control();

            var menu = new MainMenu();
            root.AddChild(menu);
            menu.Bind(context.GameManager, context.Bus);
            menu.Open();
            AssertOverlayFits(menu, viewportSize, "Main menu");

            var inventory = new InventoryUI();
            root.AddChild(inventory);
            inventory.Bind(context.GameManager, context.Bus, context.Content, new Tooltip());
            inventory.Open();
            AssertOverlayFits(inventory, viewportSize, "Inventory");

            var sheet = new CharacterSheet();
            root.AddChild(sheet);
            sheet.Bind(context.GameManager, context.Bus, context.Content);
            sheet.Open();
            AssertOverlayFits(sheet, viewportSize, "Character sheet");

            var workbench = new DevToolsWorkbench();
            root.AddChild(workbench);
            workbench.Bind(context.GameManager, context.Bus, context.Content);
            workbench.Open();
            AssertOverlayFits(workbench, viewportSize, "Developer workshop");
        });
    }

    private static void UIRootRoutesKeyboard()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        Expect.True(root.MainMenu.Visible, "Main menu should start visible while the game manager is in MainMenu state");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

        Expect.Equal(GameManager.GameState.Playing, context.GameManager.CurrentState, "Selecting New Game should transition into playing state");
        Expect.False(root.MainMenu.Visible, "Main menu should close once a new game starts");
        Expect.True(root.Minimap.Visible, "Gameplay should show the minimap by default.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Tab });
        Expect.False(root.Minimap.Visible, "The minimap toggle should hide the overlay during gameplay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.M });
        Expect.True(root.Minimap.Visible, "The alternate minimap hotkey should restore the overlay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.I });
        Expect.True(root.Inventory.Visible, "Gameplay input should open the inventory overlay");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.Inventory.Visible, "Escape should close the inventory before reaching pause handling");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.True(root.PauseMenu.Visible, "Escape from gameplay should open the pause menu");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.PauseMenu.Visible, "Escape from the pause menu should close the overlay");

        context.GameManager.World!.Player.Stats.HP = 0;
        context.Bus.EmitTurnCompleted();
        Expect.True(root.GameOverScreen.Visible, "A dead player should open the game over overlay on the next turn update");
    }

    private static UIContext CreateContext(params ItemInstance[] items)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(6, 6), TileType.StairsDown);

        var player = new StubEntity(
            "Player",
            new Position(1, 1),
            Faction.Player,
            stats: new Stats
            {
                HP = 40,
                MaxHP = 40,
                Attack = 8,
                Defense = 4,
                Accuracy = 80,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });
        var inventory = new InventoryComponent(20);
        if (items.Length == 0)
        {
            items = new[]
            {
                new ItemInstance { TemplateId = "potion_health", IsIdentified = true },
                new ItemInstance { TemplateId = "potion_health", IsIdentified = true },
            };
        }

        foreach (var item in items)
        {
            inventory.Add(item);
        }

        player.SetComponent(inventory);

        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new UIContext(world, player, bus, gameManager, content);
    }

    private sealed record UIContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);

    private static void WithViewportSize(Vector2 viewportSize, System.Action<Vector2> action)
    {
        var viewport = new Control().GetViewport();
        var originalSize = viewport.Size;
        viewport.Size = viewportSize;

        try
        {
            action(viewportSize);
        }
        finally
        {
            viewport.Size = originalSize;
        }
    }

    private static void AssertOverlayFits(Control overlay, Vector2 viewportSize, string overlayName)
    {
        Expect.True(overlay.Children.Count > 0 && overlay.Children[0] is Control, $"{overlayName} should create a root panel.");
        var panel = (Control)overlay.Children[0];

        Expect.True(panel.Position.X >= 0f && panel.Position.Y >= 0f, $"{overlayName} panel should stay within the viewport origin.");
        Expect.True(panel.Position.X + panel.Size.X <= viewportSize.X + 0.1f, $"{overlayName} panel should fit horizontally within the viewport.");
        Expect.True(panel.Position.Y + panel.Size.Y <= viewportSize.Y + 0.1f, $"{overlayName} panel should fit vertically within the viewport.");

        for (var i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not Control child)
            {
                continue;
            }

            Expect.True(child.Position.X >= 0f && child.Position.Y >= 0f, $"{overlayName} child controls should remain inside the panel origin.");
            Expect.True(child.Position.X + child.Size.X <= panel.Size.X + 0.1f, $"{overlayName} child controls should fit horizontally within the panel.");
            Expect.True(child.Position.Y + child.Size.Y <= panel.Size.Y + 0.1f, $"{overlayName} child controls should fit vertically within the panel.");
        }
    }

    private static int CountVisibleTiles(IWorldState world)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.IsVisible(new Position(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }
}