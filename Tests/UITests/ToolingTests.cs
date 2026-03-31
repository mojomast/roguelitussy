using System;
using System.IO;
using System.Text.Json;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class ToolingTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Tools.MapEditor saves and reloads prefabs", MapEditorRoundTripsPrefabs);
        registry.Add("UI.Tools.ItemEditor saves item and enemy content", ItemEditorSavesContent);
        registry.Add("UI.Tools.DebugConsole executes runtime commands", DebugConsoleExecutesCommands);
        registry.Add("UI.Tools.Plugin registers editor docks", PluginRegistersDocks);
        registry.Add("UI.Tools.UIRoot toggles debug overlays", UIRootTogglesDebugTools);
        registry.Add("UI.Tools.Workbench authors content without Godot editor", WorkbenchAuthorsContentWithoutEditor);
        registry.Add("UI.Tools.Workbench opens from title and gameplay", UIRootOpensWorkbench);
        registry.Add("UI.Tools.Workbench playtests drafts and reloads runtime content", WorkbenchPlaytestsAndReloadsContent);
        registry.Add("UI.Tools.Workbench manages runtime sessions and saves", WorkbenchManagesRuntimeSessionsAndSaves);
    }

    private static void MapEditorRoundTripsPrefabs()
    {
        var contentDirectory = CreateTemporaryContentDirectory();
        try
        {
            var editor = new MapEditor();
            editor.ResizeCanvas(5, 4);
            editor.SetMetadata("tool_test_room", "Tool Test Room", "tools, test", 2, 5);
            editor.PaintRectangle(0, 0, 4, 3, '#');
            editor.PaintCell(1, 1, '.');
            editor.PaintCell(2, 1, '.');
            editor.PaintCell(0, 1, '+');
            editor.PaintCell(3, 2, 'S');
            editor.PaintCell(2, 3, '>');

            editor.SavePrefab(contentDirectory);

            var reloaded = new MapEditor();
            var found = reloaded.LoadPrefab("tool_test_room", contentDirectory);

            Expect.True(found, "Map editor should reload a saved prefab by id");
            Expect.Equal('+', reloaded.GetCell(0, 1), "Door cells should survive a save/load round trip");
            Expect.Equal('S', reloaded.GetCell(3, 2), "Spawn markers should survive a save/load round trip");
            Expect.Equal('>', reloaded.GetCell(2, 3), "Stairs markers should survive a save/load round trip");
        }
        finally
        {
            DeleteDirectory(contentDirectory);
        }
    }

    private static void ItemEditorSavesContent()
    {
        var contentDirectory = CreateTemporaryContentDirectory();
        try
        {
            var editor = new ItemEditor();
            editor.Load(contentDirectory);

            var item = editor.CreateItem("tool_test_tonic");
            item.Description = "Temporary tonic for tooling tests.";
            item.Type = "consumable";
            item.Slot = "none";
            item.Stackable = true;
            item.MaxStack = 3;
            item.SpritePath = "res://Assets/Sprites/items/potion_health.png";
            editor.UpsertItem(item);

            var enemy = editor.CreateEnemy("tool_test_dummy");
            enemy.Description = "Training dummy for tooling tests.";
            enemy.AiType = "melee_rush";
            enemy.Faction = "Enemy";
            enemy.SpritePath = "res://Assets/Sprites/enemies/rat.png";
            editor.UpsertEnemy(enemy);

            var validationErrors = editor.ValidateAll();
            Expect.Equal(0, validationErrors.Count, "Editor-created content should pass local validation");

            editor.SaveItems(contentDirectory);
            editor.SaveEnemies(contentDirectory);

            var items = ReadDocument<ItemsDocument>(Path.Combine(contentDirectory, "items.json"));
            var enemies = ReadDocument<EnemiesDocument>(Path.Combine(contentDirectory, "enemies.json"));

            Expect.True(items.Items.Exists(entry => entry.Id == "tool_test_tonic"), "Saved item document should contain the new item");
            Expect.True(enemies.Enemies.Exists(entry => entry.Id == "tool_test_dummy"), "Saved enemy document should contain the new enemy");
        }
        finally
        {
            DeleteDirectory(contentDirectory);
        }
    }

    private static void DebugConsoleExecutesCommands()
    {
        var context = CreateContext();
        var console = new DebugConsole();
        console.Bind(context.GameManager, context.Bus, context.Content);

        context.Player.Stats.HP = 10;
        var healResult = console.SubmitCommand("heal 5");
        var spawnResult = console.SubmitCommand("spawn_item potion_health 2");
        var teleportResult = console.SubmitCommand("teleport 2 2");
        var saveResult = console.SubmitCommand("save 1");
        var loadResult = console.SubmitCommand("load 1");

        Expect.True(healResult.Contains("15/40"), "Heal command should update player HP");
        Expect.True(spawnResult.Contains("Added 2x"), "Spawn item command should add items to inventory");
        Expect.Equal(new Position(2, 2), context.Player.Position, "Teleport command should move the player");
        Expect.True(saveResult.Contains("Saved slot 1"), "Save command should use the save manager");
        Expect.True(loadResult.Contains("Loaded slot 1"), "Load command should reload from the save manager");
    }

    private static void PluginRegistersDocks()
    {
        var plugin = new RoguelikeToolsPlugin();
        plugin._EnterTree();

        Expect.Equal(2, plugin.BottomPanelControls.Count, "Plugin should register both editor docks");
        Expect.NotNull(plugin.MapEditorDock, "Map editor dock should be created on enter tree");
        Expect.NotNull(plugin.ItemEditorDock, "Item editor dock should be created on enter tree");

        plugin._ExitTree();

        Expect.Equal(0, plugin.BottomPanelControls.Count, "Plugin should remove docks on exit tree");
    }

    private static void UIRootTogglesDebugTools()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Backquote });

        Expect.True(root.DebugConsole.Visible, "Backquote should open the debug console");

        root.DebugConsole.PendingInput = "heal 5";
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

        Expect.Equal(40, context.Player.Stats.HP, "Console commands should execute while the console is open");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Q });

        Expect.True(root.DebugOverlay.Visible, "Q should toggle the debug overlay");
    }

    private static void WorkbenchAuthorsContentWithoutEditor()
    {
        var contentDirectory = CreateTemporaryContentDirectory();
        try
        {
            var context = CreateContext();
            var workbench = new DevToolsWorkbench();
            workbench.Bind(context.GameManager, context.Bus, context.Content, contentDirectory);
            workbench.Open();

            workbench.CreateRoomDraft();
            workbench.SaveCurrentRoomDraft();
            workbench.CreateItemDraft();
            workbench.SaveItemsDocument();
            workbench.CreateEnemyDraft();
            workbench.SaveEnemiesDocument();

            var rooms = ReadDocument<RoomPrefabsDocument>(Path.Combine(contentDirectory, "room_prefabs.json"));
            var items = ReadDocument<ItemsDocument>(Path.Combine(contentDirectory, "items.json"));
            var enemies = ReadDocument<EnemiesDocument>(Path.Combine(contentDirectory, "enemies.json"));

            Expect.True(rooms.Rooms.Exists(room => room.Id.StartsWith("custom_room_", StringComparison.Ordinal)), "Workbench should be able to save a new room draft.");
            Expect.True(items.Items.Exists(item => item.Id.StartsWith("custom_item_", StringComparison.Ordinal)), "Workbench should be able to save a new item draft.");
            Expect.True(enemies.Enemies.Exists(enemy => enemy.Id.StartsWith("custom_enemy_", StringComparison.Ordinal)), "Workbench should be able to save a new enemy draft.");
        }
        finally
        {
            DeleteDirectory(contentDirectory);
        }
    }

    private static void UIRootOpensWorkbench()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.T });
        Expect.True(root.DevToolsWorkbench.Visible, "The title flow should be able to open the developer workshop.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.DevToolsWorkbench.Visible, "Escape should close the developer workshop.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.T });
        Expect.True(root.DevToolsWorkbench.Visible, "Gameplay should also be able to open the developer workshop.");
    }

    private static void WorkbenchPlaytestsAndReloadsContent()
    {
        var contentDirectory = CreateTemporaryContentDirectory();
        try
        {
            var context = CreateContext();
            var workbench = new DevToolsWorkbench();
            var playtestStarted = false;
            IContentDatabase? reloaded = null;

            workbench.PlaytestStarted += () => playtestStarted = true;
            workbench.RuntimeContentReloaded += content => reloaded = content;
            workbench.Bind(context.GameManager, context.Bus, context.Content, contentDirectory);
            workbench.Open();

            workbench.CreateRoomDraft();
            var roomStarted = workbench.PlaytestCurrentRoomDraft();

            Expect.True(roomStarted, "Workbench should be able to load the current room draft into a playable world.");
            Expect.True(playtestStarted, "Room playtests should notify the UI shell.");
            Expect.Equal(GameManager.GameState.Playing, context.GameManager.CurrentState, "Playing a room draft should enter gameplay state.");
            Expect.Equal(20, context.GameManager.World!.Width, "Default room drafts should load with the map editor dimensions.");

            workbench.Open();
            var errors = workbench.ReloadRuntimeContent();
            var itemDropped = workbench.DropSelectedItemAtPlayer();
            var enemySpawned = workbench.SpawnSelectedEnemyNearPlayer();

            Expect.True(errors.Count >= 0, "Reloading runtime content should return a warning list rather than throwing.");
            Expect.True(itemDropped, "Workbench should be able to drop the selected item into the active run.");
            Expect.True(enemySpawned, "Workbench should be able to spawn the selected enemy into the active run.");
            Expect.True(context.GameManager.World.HasGroundItems(context.GameManager.World.Player.Position), "Dropped workshop items should appear on the ground.");
            Expect.True(context.GameManager.World.Entities.Count >= 2, "Spawned workshop enemies should appear in the active run.");

            var itemEditor = new ItemEditor();
            itemEditor.Load(contentDirectory);
            itemEditor.CreateItem("workbench_reload_item");
            itemEditor.SaveItems(contentDirectory);

            errors = workbench.ReloadRuntimeContent();
            Expect.NotNull(reloaded, "Workbench should notify listeners after a successful runtime content reload.");
            Expect.True(context.GameManager.Content!.TryGetItemTemplate("workbench_reload_item", out _), "Runtime content reload should replace the game manager database.");
        }
        finally
        {
            DeleteDirectory(contentDirectory);
        }
    }

    private static void WorkbenchManagesRuntimeSessionsAndSaves()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        root.DevToolsWorkbench.Open();
        root.DevToolsWorkbench.SetSelectedSaveSlot(2);
        context.Player.Stats.HP = 17;
        context.World.Depth = 3;

        var saved = root.DevToolsWorkbench.SaveCurrentRunToSlot();
        Expect.True(saved, "Workbench should be able to save the active run to the selected slot.");

        root.DevToolsWorkbench.SetPendingSeed(2468);
        var started = root.DevToolsWorkbench.StartNewExpeditionFromSeed();

        Expect.True(started, "Workbench should be able to launch a new expedition from the selected seed.");
        Expect.False(root.MainMenu.Visible, "Starting a run from the workshop should dismiss the title shell.");
        Expect.Equal(2468, context.GameManager.Seed, "Workshop run control should apply the selected seed.");

        var healed = root.DevToolsWorkbench.HealPlayerToFull();
        var revealed = root.DevToolsWorkbench.RevealEntireMap();

        Expect.True(healed, "Workbench should heal the active player without using the debug console.");
        Expect.Equal(context.GameManager.World!.Player.Stats.MaxHP, context.GameManager.World.Player.Stats.HP, "Healing should restore the player to full HP.");
        Expect.True(revealed, "Workbench should reveal the entire active map.");
        Expect.Equal(context.GameManager.World.Width * context.GameManager.World.Height, CountVisibleTiles(context.GameManager.World), "Reveal should expose every tile in the active world.");

        root.DevToolsWorkbench.SetPendingFloorDepth(4);
        var travelled = root.DevToolsWorkbench.TravelToPendingFloor();

        Expect.True(travelled, "Workbench should support direct floor travel without the debug console.");
        Expect.Equal(4, context.GameManager.World.Depth, "Floor travel should move the active run to the selected depth.");

        root.DevToolsWorkbench.SetPendingTeleportTarget(2, 2);
        var teleported = root.DevToolsWorkbench.TeleportPlayerToPendingTarget();

        Expect.True(teleported, "Workbench should support direct player teleport within the active world.");
        Expect.Equal(new Position(2, 2), context.GameManager.World.Player.Position, "Teleport should move the player to the selected tile.");

        var restored = root.DevToolsWorkbench.RestorePlayerFog();

        Expect.True(restored, "Workbench should be able to restore normal player visibility after a reveal.");
        Expect.True(CountVisibleTiles(context.GameManager.World) < context.GameManager.World.Width * context.GameManager.World.Height, "Restoring fog should return to a player-limited visibility set.");

        root.DevToolsWorkbench.Open();
        var loaded = root.DevToolsWorkbench.LoadRunFromSlot();

        Expect.True(loaded, "Workbench should be able to load the selected save slot.");
        Expect.Equal(3, context.GameManager.World!.Depth, "Loading from the workshop should restore the saved world depth.");
        Expect.Equal(17, context.GameManager.World.Player.Stats.HP, "Loading from the workshop should restore the saved player state.");
    }

    private static ToolContext CreateContext()
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

        world.SetTile(new Position(4, 4), TileType.Door);

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
                Accuracy = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });
        player.SetComponent(new InventoryComponent(20));

        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        var saveManager = new StubSaveManager();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, saveManager, bus);

        return new ToolContext(world, player, bus, gameManager, content);
    }

    private static string CreateTemporaryContentDirectory()
    {
        var sourceDirectory = ContentLoader.FindContentDirectory();
        var targetDirectory = Path.Combine(Path.GetTempPath(), "godotussy-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        foreach (var fileName in RequiredContentFiles)
        {
            File.Copy(Path.Combine(sourceDirectory, fileName), Path.Combine(targetDirectory, fileName), overwrite: true);
        }

        return targetDirectory;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static readonly string[] RequiredContentFiles =
    {
        "items.json",
        "enemies.json",
        "abilities.json",
        "status_effects.json",
        "room_prefabs.json",
        "loot_tables.json",
    };

    private static T ReadDocument<T>(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<T>(json);
        if (document is null)
        {
            throw new InvalidOperationException($"Failed to deserialize '{path}'.");
        }

        return document;
    }

    private sealed record ToolContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);

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