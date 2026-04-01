using System.Linq;
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
        registry.Add("UI.GameManager floor travel succeeds when arrival stair is occupied", GameManagerFloorTravelFindsNearbyArrival);
        registry.Add("UI.GameManager floor travel restores mutated arrival stairs", GameManagerFloorTravelRepairsMutatedArrivalTile);
        registry.Add("UI.HUD updates from bus and exposes keyboard toggles", HudUpdatesFromBus);
        registry.Add("UI.Minimap reflects explored tiles and gameplay toggles", MinimapReflectsExplorationAndToggles);
        registry.Add("UI.MainMenu character creation affects the starting run", MainMenuCharacterCreationAffectsStartingRun);
        registry.Add("UI.GameManager enemy turns resolve after player action", GameManagerEnemyTurnsResolveAfterPlayerAction);
        registry.Add("UI.Inventory keyboard navigation emits concrete actions", InventoryEmitsConcreteActions);
        registry.Add("UI.Inventory and tooltip surface item rarity", InventorySurfacesItemRarity);
        registry.Add("UI.Help overlay opens from menu and gameplay", HelpOverlayOpensFromMenuAndGameplay);
        registry.Add("UI.CombatLog escapes BBCode and reacts to combat events", CombatLogReactsToEvents);
        registry.Add("UI.CombatLog exposes a live console panel", CombatLogExposesLiveConsolePanel);
        registry.Add("UI.Chest interactions report loot to the combat log", ChestInteractionsReportLootToCombatLog);
        registry.Add("UI.Overlays clamp to the viewport", OverlaysClampToViewport);
        registry.Add("UI.MainMenu scrolls option list within the viewport", MainMenuScrollsOptionListWithinViewport);
        registry.Add("UI.Dialog and shop overlays route interaction flow", DialogAndShopOverlaysRouteInteractionFlow);
        registry.Add("UI.Shop applies perk-based merchant discounts", ShopAppliesPerkDiscounts);
        registry.Add("UI.Dialog overlay traverses authored advisor dialog nodes", DialogOverlayTraversesAdvisorDialogNodes);
        registry.Add("UI.GameManager honors detailed authored generator spawns", GameManagerHonorsDetailedAuthoredGeneratorSpawns);
        registry.Add("UI.GameManager starts correctly with repository content", GameManagerStartsWithRepositoryContent);
        registry.Add("UI.GameManager seed 1337 descends cleanly with repository content", GameManagerRepositorySeed1337DescendsCleanly);
        registry.Add("UI.GameManager can self-initialize runtime services on new game", GameManagerSelfInitializesRuntimeServicesOnNewGame);
        registry.Add("UI.UIRoot binds safely before the first run starts", UIRootBindsSafelyBeforeRunStart);
        registry.Add("UI.UIRoot resolves named autoloads from the viewport", UIRootResolvesNamedViewportAutoloads);
        registry.Add("UI.UIRoot accepts numpad enter on the title screen", UIRootAcceptsNumpadEnter);
        registry.Add("UI.UIRoot ignores repeated key echo events", UIRootIgnoresEchoedKeys);
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
        context.Player.SetComponent(new ProgressionComponent
        {
            Level = 3,
            Experience = 24,
            ExperienceToNextLevel = 40,
            UnspentStatPoints = 1,
        });
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
        Expect.True(hud.StatsText.Contains("ATK 8"), "HUD should expose the player's combat stats.");
        Expect.True(hud.LevelText.Contains("XP: 24/40"), "HUD should expose level progression and experience.");
        Expect.Equal(Colors.Red.R, hud.HPColor.R, "Low HP should switch the HUD into the red threshold");
        Expect.True(hud.Children.Count > 0 && hud.Children[0] is Panel, "HUD should create a visible panel container.");

        var before = hud.MinimapText;
        hud.ToggleMinimap();
        Expect.False(before == hud.MinimapText, "Toggling the minimap should change the HUD summary");
    }

    private static void GameManagerEnemyTurnsResolveAfterPlayerAction()
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(2, 2), Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var enemy = new StubEntity("Goblin", new Position(3, 2), Faction.Enemy,
            stats: new Stats { HP = 10, MaxHP = 10, Attack = 5, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        enemy.SetComponent<IBrain>(new MeleeRusherBrain());

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var outcome = gameManager.ProcessPlayerAction(new WaitAction(player.Id));

        Expect.Equal(ActionResult.Success, outcome.Result, "Waiting should still succeed as the player action.");
        Expect.True(
            player.Stats.HP < player.Stats.MaxHP
            || outcome.LogMessages.Exists(message => message.Contains("Goblin") && (message.Contains("hits Player") || message.Contains("misses Player"))),
            "Enemy response turns should resolve after the player acts.");
    }

    private static void GameManagerFloorTravelFindsNearbyArrival()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);
        gameManager.StartNewGame(4040);

        var player = gameManager.World!.Player;
        gameManager.World.MoveEntity(player.Id, new Position(8, 8));
        var descend = gameManager.ProcessPlayerAction(new DescendAction(player.Id));
        Expect.Equal(ActionResult.Success, descend.Result, "Initial descend should succeed.");

        gameManager.World.MoveEntity(gameManager.World.Player.Id, new Position(2, 1));
        var occupant = new StubEntity("Blocker", new Position(1, 1), Faction.Enemy);
        gameManager.World.AddEntity(occupant);

        Expect.True(gameManager.TravelToFloor(0), "Returning to the previous floor should succeed.");

        var returnPlayer = gameManager.World!.Player;
        gameManager.World.MoveEntity(returnPlayer.Id, new Position(8, 8));
        var descendAgain = gameManager.ProcessPlayerAction(new DescendAction(returnPlayer.Id));

        Expect.Equal(ActionResult.Success, descendAgain.Result, "Descending to a cached floor with an occupied arrival tile should still succeed.");
        Expect.Equal(1, gameManager.World!.Depth, "Player should arrive back on floor one.");
        Expect.True(gameManager.World.Player.Position != new Position(1, 1), "Player should be placed on a nearby open tile when the stair tile is occupied.");
    }

    private static void GameManagerFloorTravelRepairsMutatedArrivalTile()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);
        gameManager.StartNewGame(5050);

        var player = gameManager.World!.Player;
        gameManager.World.MoveEntity(player.Id, new Position(8, 8));
        var descend = gameManager.ProcessPlayerAction(new DescendAction(player.Id));
        Expect.Equal(ActionResult.Success, descend.Result, "Initial descend should succeed.");

        gameManager.World.SetTile(new Position(1, 1), TileType.Wall);

        Expect.True(gameManager.TravelToFloor(0), "Returning to the previous floor should still succeed after mutating the cached destination floor.");

        var returnPlayer = gameManager.World!.Player;
        gameManager.World.MoveEntity(returnPlayer.Id, new Position(8, 8));
        var descendAgain = gameManager.ProcessPlayerAction(new DescendAction(returnPlayer.Id));

        Expect.Equal(ActionResult.Success, descendAgain.Result, "Descending should repair a mutated arrival stair tile instead of failing the floor transition.");
        Expect.Equal(TileType.StairsUp, gameManager.World!.GetTile(new Position(1, 1)), "Travel should restore the expected stair tile when the cached arrival tile was overwritten.");
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

    private static void InventorySurfacesItemRarity()
    {
        WithViewportSize(new Vector2(640f, 360f), viewportSize =>
        {
            var context = CreateContext(new ItemInstance { TemplateId = "scroll_fireball", IsIdentified = true });
            var root = new Control();
            var tooltip = new Tooltip();
            var inventory = new InventoryUI();

            root.AddChild(tooltip);
            root.AddChild(inventory);
            inventory.Bind(context.GameManager, context.Bus, context.Content, tooltip);
            inventory.Open();

            Expect.True(inventory.DescriptionText.Contains("Rarity: Rare"), "Inventory descriptions should expose the selected item's rarity tier.");
            Expect.True(inventory.GridMarkup.Contains("[color="), "Inventory grid markup should colorize loot by rarity.");
            Expect.True(tooltip.BodyText.Contains("Rarity: Rare"), "Item tooltips should expose rarity details.");
            Expect.True(tooltip.TitleMarkup.Contains("[color="), "Item tooltip titles should be colorized by rarity.");
            Expect.Equal(new Vector2(viewportSize.X - tooltip.Size.X - 24f, viewportSize.Y - tooltip.Size.Y - 24f), tooltip.ScreenPosition,
                "Inventory comparison and detail tooltips should anchor to the bottom-right corner of the viewport.");
        });
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
        var identity = player.GetComponent<IdentityComponent>();
        Expect.NotNull(identity, "Character creation should attach an identity component to the player.");
        Expect.Equal("elf", identity!.RaceId, "Character creation should persist the selected race.");
        Expect.Equal("masculine", identity.GenderId, "Character creation should persist the selected gender.");
        Expect.Equal("scarred", identity.AppearanceId, "Character creation should persist the selected appearance.");
        Expect.Equal("elf_masculine_scarred_skirmisher", identity.SpriteVariantId, "Character creation should compose the full sprite variant id.");
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
        context.Bus.EmitItemPickedUp(context.Player.Id, new ItemInstance { TemplateId = "scroll_fireball", IsIdentified = true });

        Expect.True(log.RenderedText.Contains("[lb]trap[rb]"), "Combat log should escape BBCode brackets");
        Expect.True(log.RenderedText.Contains("hits Skeleton for 4 damage"), "Combat log should append derived combat messages");
        Expect.True(log.RenderedText.Contains("rare loot"), "Combat log should call out higher-rarity pickups.");
        Expect.True(log.RenderedText.Contains("Scroll of Fireball"), "Combat log should include the picked-up item name.");
    }

    private static void CombatLogExposesLiveConsolePanel()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);

        var root = new Control();
        var log = new CombatLog();
        root.AddChild(log);
        log._Ready();
        log.Bind(context.GameManager, context.Bus);

        context.Bus.EmitLogMessage("Console live.");

        Expect.True(log.Children.Count > 0 && log.Children[0] is Control, "Combat log should create a visible console panel.");
        Expect.True(log.RenderedText.Contains("Console live."), "Combat log should keep rendering live updates from the event stream.");
    }

    private static void ChestInteractionsReportLootToCombatLog()
    {
        var bus = new EventBus();
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Depth = 1;
        world.Seed = 9001;
        world.ContentDatabase = content;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 2, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        player.SetComponent(new InventoryComponent(20));
        player.SetComponent(new WalletComponent { Gold = 120 });
        world.Player = player;
        world.AddEntity(player);

        var chest = new Entity("Treasure Chest", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0 }, Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "floor_loot" });
        world.AddEntity(chest);

        var gameManager = new GameManager();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        gameManager.LoadWorld(world);

        var root = new UIRoot();
        root.BindServices(gameManager, bus, content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Right });

        Expect.True(root.CombatLog.RenderedText.Contains("opens the chest", System.StringComparison.OrdinalIgnoreCase),
            "Opening a chest through gameplay input should add an explicit loot message to the combat log.");
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

            var help = new HelpOverlay();
            root.AddChild(help);
            help.OpenGameplayHelp();
            AssertOverlayFits(help, viewportSize, "Help overlay");
        });
    }

    private static void MainMenuScrollsOptionListWithinViewport()
    {
        WithViewportSize(new Vector2(640f, 360f), _ =>
        {
            var gameManager = new GameManager();
            var bus = new EventBus();
            gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

            var root = new Control();
            var menu = new MainMenu();
            root.AddChild(menu);
            menu.Bind(gameManager, bus);
            menu.Open();

            for (var i = 0; i < 18; i++)
            {
                menu.HandleKey(Key.Down);
            }

            Expect.True(menu.MenuText.Contains("> Quit"), "The selected bottom main-menu option should remain visible after scrolling.");
            Expect.False(menu.MenuText.Contains("Start Expedition\n") || menu.MenuText.Contains("> Start Expedition"), "The rendered menu text should window the option list instead of keeping off-screen top entries visible.");
        });
    }

    private static void GameManagerStartsWithRepositoryContent()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();

        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new DungeonGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        gameManager.StartNewGame(2025);

        Expect.Equal(GameManager.GameState.Playing, gameManager.CurrentState, "Repository content should support launching a real generated run.");
        Expect.NotNull(gameManager.World, "Launching with repository content should create a world.");
        Expect.NotNull(gameManager.World!.Player, "Launching with repository content should spawn a player.");
    }

    private static void GameManagerRepositorySeed1337DescendsCleanly()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();

        var logs = new System.Collections.Generic.List<string>();
        bus.LogMessage += logs.Add;

        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new DungeonGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        gameManager.StartNewGame(1337);

        var world = gameManager.World!;
        var stairsDown = FindTile(world, TileType.StairsDown);
        world.MoveEntity(world.Player.Id, stairsDown);

        var outcome = gameManager.ProcessPlayerAction(new DescendAction(world.Player.Id));

        Expect.Equal(ActionResult.Success, outcome.Result, "Descending on repository seed 1337 should resolve as a valid player action.");
        Expect.Equal(1, gameManager.World!.Depth,
            $"Descending on repository seed 1337 should move the run to floor one. Logs: {string.Join(" | ", logs)}");
        Expect.False(logs.Any(message => message.Contains("Floor transition failed", System.StringComparison.Ordinal)),
            "Repository seed 1337 should not emit a floor transition failure when descending.");
    }

    private static void GameManagerSelfInitializesRuntimeServicesOnNewGame()
    {
        var gameManager = new GameManager();

        gameManager.StartNewGame(3030);

        Expect.Equal(GameManager.GameState.Playing, gameManager.CurrentState, "Starting a run without pre-attached services should lazily initialize runtime services.");
        Expect.NotNull(gameManager.World, "Lazy runtime initialization should create a world.");
        Expect.NotNull(gameManager.Content, "Lazy runtime initialization should load the content database.");
    }

    private static void UIRootResolvesNamedViewportAutoloads()
    {
        var context = CreateContext();
        var root = new UIRoot();
        var viewport = root.GetViewport();
        var gameManager = context.GameManager;
        var bus = context.Bus;
        var contentAutoload = new ContentDatabase { Name = "ContentDatabase" };

        gameManager.Name = "GameManager";
        bus.Name = "EventBus";
        contentAutoload.SetDatabase(context.Content);

        viewport.AddChild(gameManager);
        viewport.AddChild(bus);
        viewport.AddChild(contentAutoload);

        try
        {
            root._Ready();
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

            Expect.Equal(GameManager.GameState.Playing, gameManager.CurrentState, "UIRoot should bind to viewport autoloads by name when typed root lookups are unavailable.");
            Expect.False(root.MainMenu.Visible, "The title screen should close after the viewport-resolved GameManager starts a run.");
        }
        finally
        {
            viewport.RemoveChild(contentAutoload);
            viewport.RemoveChild(bus);
            viewport.RemoveChild(gameManager);
        }
    }

    private static void UIRootBindsSafelyBeforeRunStart()
    {
        var root = new UIRoot();
        var viewport = root.GetViewport();
        var gameManager = new GameManager { Name = "GameManager" };
        var bus = new EventBus { Name = "EventBus" };
        var contentAutoload = new ContentDatabase { Name = "ContentDatabase" };

        viewport.AddChild(gameManager);
        viewport.AddChild(bus);
        viewport.AddChild(contentAutoload);

        try
        {
            gameManager._Ready();
            root._Ready();

            Expect.True(root.MainMenu.Visible, "The title screen should remain visible before a run starts.");
            Expect.Equal(GameManager.GameState.MainMenu, gameManager.CurrentState, "Binding UI before a run starts should not force gameplay.");
        }
        finally
        {
            viewport.RemoveChild(contentAutoload);
            viewport.RemoveChild(bus);
            viewport.RemoveChild(gameManager);
        }
    }

    private static void UIRootAcceptsNumpadEnter()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.KpEnter });

        Expect.Equal(GameManager.GameState.Playing, context.GameManager.CurrentState, "Numpad Enter should confirm the title screen selection.");
        Expect.False(root.MainMenu.Visible, "The title screen should close after a successful numpad Enter start.");
    }

    private static void UIRootIgnoresEchoedKeys()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, Echo = true, PhysicalKeycode = Key.Enter });

        Expect.Equal(GameManager.GameState.MainMenu, context.GameManager.CurrentState, "Echoed key events should not retrigger gameplay actions.");
        Expect.True(root.MainMenu.Visible, "Echoed title-screen inputs should be ignored instead of dismissing the main menu.");
    }

    private static void UIRootRoutesKeyboard()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        Expect.True(root.MainMenu.Visible, "Main menu should start visible while the game manager is in MainMenu state");
        Expect.False(root.CombatLog.ConsoleVisible, "Main menu overlays should suppress gameplay log chrome.");

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

        var progression = new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 };
        context.GameManager.World!.Player.SetComponent(progression);
        context.Bus.EmitProgressionChanged(context.GameManager.World.Player.Id);
        Expect.True(root.LevelUpOverlay.Visible, "Pending perk choices should open the dedicated level-up overlay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        Expect.False(root.LevelUpOverlay.Visible, "Selecting a perk should close the overlay once no choices remain.");
        Expect.True(progression.SelectedPerkIds.Count == 1, "Level-up overlay input should select a perk through the game manager.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.True(root.PauseMenu.Visible, "Escape from gameplay should open the pause menu");
        Expect.False(root.Minimap.Visible, "Pause overlays should suppress the gameplay minimap.");
        Expect.False(root.CombatLog.ConsoleVisible, "Pause overlays should suppress the gameplay combat log.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.PauseMenu.Visible, "Escape from the pause menu should close the overlay");
        Expect.True(root.Minimap.Visible, "Closing pause should restore the gameplay minimap.");
        Expect.True(root.CombatLog.ConsoleVisible, "Closing pause should restore the gameplay combat log.");

        context.GameManager.World!.Player.Stats.HP = 0;
        context.Bus.EmitTurnCompleted();
        Expect.True(root.GameOverScreen.Visible, "A dead player should open the game over overlay on the next turn update");
    }

    private static void DialogAndShopOverlaysRouteInteractionFlow()
    {
        var context = CreateContext(new ItemInstance { TemplateId = "scroll_blink", IsIdentified = true });
        context.GameManager.LoadWorld(context.World);
        var merchant = CreateMerchant(context.Content, new Position(2, 1));
        context.World.AddEntity(merchant);

        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        var wallet = context.Player.GetComponent<WalletComponent>()!;
        var goldBefore = wallet.Gold;

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Right });
        Expect.False(root.DialogUI.Visible, "Walking into an adjacent neutral NPC should no longer open dialog automatically.");
        Expect.Equal(new Position(2, 1), context.Player.Position, "Bumping into a merchant should swap the player into the occupied tile.");
        Expect.Equal(new Position(1, 1), merchant.Position, "The neutral NPC should swap back into the player's old tile.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.F });
        Expect.True(root.DialogUI.Visible, "The dedicated interact key should open dialog when an NPC is adjacent.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        Expect.False(root.DialogUI.Visible, "Selecting the first merchant dialog option should leave the dialog overlay.");
        Expect.True(root.ShopUI.Visible, "Merchant dialog should be able to open the shop overlay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

        Expect.True(wallet.Gold < goldBefore, "Buying from the shop should reduce player gold.");
        Expect.True(context.Player.GetComponent<InventoryComponent>()!.Items.Count >= 2, "Buying from the shop should add or stack inventory items.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.F });
        Expect.False(root.ShopUI.Visible, "Interact should close an open shop overlay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.E });
        Expect.True(root.DialogUI.Visible, "The dedicated interact key should also open dialog when an NPC is adjacent.");
    }

    private static void ShopAppliesPerkDiscounts()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        context.World.AddEntity(CreateMerchant(context.Content, new Position(2, 1)));

        var progression = new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 };
        context.Player.SetComponent(progression);
        Expect.True(context.GameManager.TrySelectPerk("quartermasters_eye", out _), "Discount perk selection should succeed.");

        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        var wallet = context.Player.GetComponent<WalletComponent>()!;
        wallet.Gold = 100;

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.F });
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

        Expect.True(root.ShopUI.SnapshotBodyMarkup().Contains("19g"), "Shop UI should display perk-adjusted buy prices.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        Expect.Equal(81, wallet.Gold, "Quartermaster's Eye should reduce a 24 gold item to 19 gold.");
    }

    private static void DialogOverlayTraversesAdvisorDialogNodes()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        context.World.AddEntity(CreateAdvisor(context.Content, new Position(2, 1)));

        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.F });
        Expect.True(root.DialogUI.Visible, "Interact should open dialog when an advisor is adjacent.");
        Expect.True(GetDialogBodyText(root.DialogUI).Contains("carry fewer things", System.StringComparison.OrdinalIgnoreCase),
            "Advisor dialog should start on the authored intro node.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        Expect.True(root.DialogUI.Visible, "Following a next-node dialog option should keep the dialog open.");
        Expect.True(GetDialogBodyText(root.DialogUI).Contains("clean bag means faster decisions", System.StringComparison.OrdinalIgnoreCase),
            "Selecting the authored advisor follow-up should advance to the next dialog node.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        Expect.False(root.DialogUI.Visible, "Selecting the authored closing option should close the advisor dialog.");
    }

    private static void GameManagerHonorsDetailedAuthoredGeneratorSpawns()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        var content = new StubContentDatabase();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new DetailedSpawnGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        gameManager.StartNewGame(2026);

        var world = gameManager.World!;
        var authoredNpc = world.Entities.FirstOrDefault(entity => entity.GetComponent<NpcComponent>()?.TemplateId == "field_chronicler");
        var authoredEnemy = world.GetEntityAt(new Position(6, 6));
        var authoredChest = world.GetEntityAt(new Position(5, 4));

        Expect.NotNull(authoredNpc, "Authored NPC spawn details should materialize into runtime NPC entities.");
        Expect.Equal(new Position(2, 2), authoredNpc!.Position, "Authored NPC spawn details should preserve their authored positions.");
        Expect.Equal("Goblin", authoredEnemy?.Name ?? string.Empty, "Authored enemy spawn details should preserve exact enemy templates.");
        Expect.True(world.GetItemsAt(new Position(4, 4)).Any(item => item.TemplateId == "shield_wooden"),
            "Authored item spawn details should preserve exact item templates.");
        Expect.Equal("deep_floor_loot", authoredChest?.GetComponent<ChestComponent>()?.LootTableId ?? string.Empty,
            "Authored chest spawn details should preserve authored loot tables.");
        Expect.Equal(1, world.Entities.Count(entity => entity.GetComponent<NpcComponent>()?.TemplateId == "field_chronicler"),
            "Ambient NPC spawning should not duplicate already-authored NPC templates.");
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
        player.SetComponent(new WalletComponent { Gold = 120 });

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

    private static Position FindTile(IWorldState world, TileType tileType)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) == tileType)
                {
                    return position;
                }
            }
        }

        throw new System.InvalidOperationException($"Tile {tileType} was not found.");
    }

    private static Entity CreateMerchant(StubContentDatabase content, Position position)
    {
        content.TryGetNpcTemplate("quartermaster", out var template);
        var merchant = new Entity(
            template.DisplayName,
            position,
            new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 6 },
            Faction.Neutral);
        merchant.SetComponent(new NpcComponent
        {
            TemplateId = template.TemplateId,
            Role = template.Role,
            DialogueId = template.DialogueId,
        });
        merchant.SetComponent(new MerchantComponent(new[]
        {
            new MerchantOfferState { ItemTemplateId = "potion_health", Price = 24, Quantity = 4 },
            new MerchantOfferState { ItemTemplateId = "shield_wooden", Price = 48, Quantity = 1 },
            new MerchantOfferState { ItemTemplateId = "sword_iron", Price = 70, Quantity = 1 },
        }));
        merchant.SetComponent(new IdentityComponent
        {
            RaceId = template.RaceId,
            GenderId = template.GenderId,
            AppearanceId = template.AppearanceId,
            SpriteVariantId = "human_neutral_weathered_vanguard",
        });
        return merchant;
    }

    private static Entity CreateAdvisor(StubContentDatabase content, Position position)
    {
        content.TryGetNpcTemplate("field_chronicler", out var template);
        var advisor = new Entity(
            template.DisplayName,
            position,
            new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 6 },
            Faction.Neutral);
        advisor.SetComponent(new NpcComponent
        {
            TemplateId = template.TemplateId,
            Role = template.Role,
            DialogueId = template.DialogueId,
        });
        advisor.SetComponent(new IdentityComponent
        {
            RaceId = template.RaceId,
            GenderId = template.GenderId,
            AppearanceId = template.AppearanceId,
            SpriteVariantId = "elf_feminine_scarred_mystic",
        });
        return advisor;
    }

    private static string GetDialogBodyText(DialogUI dialog)
    {
        var panel = FindChild<Panel>(dialog, "Panel");
        var body = panel is null ? null : FindChild<RichTextLabel>(panel, "BodyLabel");
        return body?.Text ?? string.Empty;
    }

    private static T? FindChild<T>(Node parent, string name) where T : Node
    {
        for (var index = 0; index < parent.Children.Count; index++)
        {
            if (parent.Children[index] is T typed && typed.Name == name)
            {
                return typed;
            }
        }

        return null;
    }

    private sealed class DetailedSpawnGenerator : IGenerator
    {
        public LevelData GenerateLevel(WorldState world, int seed, int depth)
        {
            world.InitGrid(10, 10);
            world.Depth = depth;
            world.Seed = seed;

            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetTile(new Position(x, y), TileType.Floor);
                }
            }

            world.SetTile(new Position(1, 1), TileType.StairsUp);
            world.SetTile(new Position(8, 8), TileType.StairsDown);

            return new LevelData(
                new Position(1, 1),
                new Position(8, 8),
                new[] { new Position(6, 6) },
                new[] { new Position(4, 4) },
                new[]
                {
                    new RoomData(0, 0, 5, 5, new Position(2, 2)),
                    new RoomData(5, 0, 5, 5, new Position(7, 2)),
                    new RoomData(0, 5, 5, 5, new Position(2, 7)),
                    new RoomData(5, 5, 5, 5, new Position(7, 7)),
                },
                new[] { new Position(5, 4) },
                new[] { new EnemySpawnData(new Position(6, 6), "goblin") },
                new[] { new ItemSpawnData(new Position(4, 4), "shield_wooden", 2) },
                new[] { new ChestSpawnData(new Position(5, 4), "deep_floor_loot") },
                new[] { new NpcSpawnData(new Position(2, 2), "field_chronicler") });
        }

        public System.Collections.Generic.IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data)
        {
            return System.Array.Empty<string>();
        }
    }

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