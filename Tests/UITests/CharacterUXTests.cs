using System.Collections.Generic;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class CharacterUXTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UX.Stat preview updates reactively in character creation", StatPreviewUpdatesReactively);
        registry.Add("UX.Stat preview matches expected base plus bonuses", StatPreviewMatchesExpected);
        registry.Add("UX.Identity preview tile updates with character choices", IdentityPreviewTileUpdates);
        registry.Add("UX.Graphical identity preview updates with character choices", GraphicalIdentityPreviewUpdates);
        registry.Add("UX.Main menu body stays compact when graphical preview is present", MainMenuBodyStaysCompact);
        registry.Add("UX.Character creation training copy shows exact effects", CharacterCreationTrainingCopyShowsExactEffects);
        registry.Add("UX.Character creation starter kit uses content descriptions", CharacterCreationStarterKitUsesContentDescriptions);
        registry.Add("UX.Character creation mystic preview notes targeted scrolls", CharacterCreationMysticPreviewNotesTargetedScrolls);
        registry.Add("UX.Main menu Tab shows starter-kit tooltip", MainMenuTabShowsStarterKitTooltip);
        registry.Add("UX.Main menu Mystic starter-kit tooltip notes targeting", MainMenuMysticStarterKitTooltipNotesTargeting);
        registry.Add("UX.Level-up spend reduces points and increases stat", LevelUpSpendReducesPointsAndIncreasesStat);
        registry.Add("UX.Level-up UI shows prompt when points available", LevelUpUIShowsPrompt);
        registry.Add("UX.Level-up overlay lists unlocked perks", LevelUpOverlayListsUnlockedPerks);
        registry.Add("UX.Level-up overlay applies selected perk", LevelUpOverlayAppliesSelectedPerk);
        registry.Add("UX.Level-up overlay uses compact right-rail layout", LevelUpOverlayUsesCompactRightRailLayout);
        registry.Add("UX.Equipment comparison generates correct delta text", EquipmentComparisonGeneratesCorrectDelta);
        registry.Add("UX.Equipment comparison shows same stats for identical items", EquipmentComparisonSameStats);
        registry.Add("UX.HUD shows level-up indicator when points available", HudShowsLevelUpIndicator);
        registry.Add("UX.Help overlay documents level-up and comparison", HelpOverlayDocumentsNewFeatures);
    }

    private static void StatPreviewUpdatesReactively()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        var preview1 = menu.BuildStatPreview();
        Expect.True(preview1.Contains("--- Stat Preview ---"), "Stat preview should contain the header.");
        Expect.True(preview1.Contains("HP:"), "Stat preview should contain HP.");
        Expect.True(preview1.Contains("ATK:"), "Stat preview should contain ATK.");

        // Change archetype (cycle to Skirmisher)
        menu.HandleKey(Key.Down);  // Name
        menu.HandleKey(Key.Down);  // Archetype
        menu.HandleKey(Key.Right); // Cycle archetype

        var preview2 = menu.BuildStatPreview();
        Expect.False(preview1 == preview2, "Stat preview should change when archetype changes.");
    }

    private static void StatPreviewMatchesExpected()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        // Default selections: Vanguard / Survivor / Iron Will / 0 training
        // Vanguard: HP+8, ATK+2, DEF+1, EVA+0, SPD-5, VR+0
        // Survivor: HP+4, ATK+0, DEF+1, EVA+0, SPD+0, VR+0
        // Iron Will: HP+4, ATK+0, DEF+1, ACC+0, EVA+0, SPD+0, VR+0
        // Base: HP=40, ATK=8, DEF=3, ACC=80, EVA=10, SPD=100, VR=8
        // Expected: HP=56, ATK=10, DEF=6, ACC=80, EVA=10, SPD=95, VR=8
        var preview = menu.BuildStatPreview();
        Expect.True(preview.Contains("HP: 56"), $"Expected HP 56 in preview. Got: {preview}");
        Expect.True(preview.Contains("ATK: 10"), $"Expected ATK 10 in preview. Got: {preview}");
        Expect.True(preview.Contains("DEF: 6"), $"Expected DEF 6 in preview. Got: {preview}");
        Expect.True(preview.Contains("ACC: 80"), $"Expected ACC 80 in preview. Got: {preview}");
        Expect.True(preview.Contains("EVA: 10"), $"Expected EVA 10 in preview. Got: {preview}");
        Expect.True(preview.Contains("SPD: 95"), $"Expected SPD 95 in preview. Got: {preview}");
        Expect.True(preview.Contains("VR: 8"), $"Expected VR 8 in preview. Got: {preview}");
    }

    private static void IdentityPreviewTileUpdates()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        var previewA = menu.BuildIdentityPreview();
        var tileA = menu.BuildPreviewTileToken();

        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);

        var previewB = menu.BuildIdentityPreview();
        var tileB = menu.BuildPreviewTileToken();

        Expect.False(previewA == previewB, "Identity preview should change when race, gender, or appearance changes.");
        Expect.False(tileA == tileB, "Preview tile token should visibly change when identity choices change.");
    }

    private static void GraphicalIdentityPreviewUpdates()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var root = new Control();
        var menu = new MainMenu();
        root.AddChild(menu);
        menu.Bind(gameManager, bus);
        menu._Ready();
        menu.Open();

        var panel = menu.Children[0] as Panel;
        Expect.NotNull(panel, "Main menu should create a root panel before rendering the preview.");

        var previewPanel = FindChild<Panel>(panel!, "PreviewPanel");
        var previewBody = FindChild<TextureRect>(previewPanel!, "PreviewBody");
        var previewTitle = FindChild<Label>(previewPanel!, "PreviewTitle");
        var previewVariant = FindChild<Label>(previewPanel!, "PreviewVariantId");

        Expect.NotNull(previewPanel, "Main menu should create a dedicated graphical preview panel.");
        Expect.NotNull(previewBody, "Graphical preview should include a body texture.");
        Expect.NotNull(previewTitle, "Graphical preview should expose a title label.");
        Expect.NotNull(previewVariant, "Graphical preview should expose a variant id label.");
        var previewTexture = previewBody!.Texture;
        Expect.True(previewTexture is not null,
            "Graphical preview body should have a loaded Texture2D.");
        Expect.False(string.IsNullOrWhiteSpace(previewTexture!.ResourcePath),
            "Graphical preview texture should resolve to a sprite asset path.");

        var beforeTitle = previewTitle!.Text;
        var beforeVariant = previewVariant!.Text;
        var beforeTint = previewBody.Modulate;

        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);
        menu.HandleKey(Key.Down);
        menu.HandleKey(Key.Right);

        Expect.False(beforeTitle == previewTitle.Text, "Graphical preview title should change when identity choices change.");
        Expect.False(beforeVariant == previewVariant.Text, "Graphical preview variant id should change when identity choices change.");
        Expect.True(previewVariant.Text.Contains("elf_masculine_scarred"), "Graphical preview should display the updated identity variant id.");
        Expect.True(beforeTint.R != previewBody.Modulate.R || beforeTint.G != previewBody.Modulate.G || beforeTint.B != previewBody.Modulate.B,
            "Graphical preview tint should change when race changes.");
    }

    private static void MainMenuBodyStaysCompact()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        Expect.False(menu.MenuText.Contains("--- Identity Preview ---"), "Main menu body should not duplicate the graphical identity preview block.");
        Expect.True(menu.MenuText.Contains("Build:"), "Main menu body should keep the compact build summary.");
        Expect.True(menu.MenuText.Contains("Identity:"), "Main menu body should keep the compact identity summary.");
    }

    private static void CharacterCreationTrainingCopyShowsExactEffects()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        Expect.True(menu.Options.Contains("Vitality (+3 Max HP): 0"), "Training option should label Vitality with its exact effect.");
        Expect.True(menu.Options.Contains("Power (+1 Attack): 0"), "Training option should label Power with its exact effect.");
        Expect.True(menu.Options.Contains("Guard (+1 Defense): 0"), "Training option should label Guard with its exact effect.");
        Expect.True(menu.Options.Contains("Finesse (+1 Accuracy/Evasion): 0"), "Training option should label Finesse with its exact effect.");
        Expect.True(menu.MenuText.Contains("VIT +3 Max HP"), "Main menu body should explain Vitality's exact training effect.");
        Expect.True(menu.MenuText.Contains("POW +1 Attack"), "Main menu body should explain Power's exact training effect.");
        Expect.True(menu.MenuText.Contains("GRD +1 Defense"), "Main menu body should explain Guard's exact training effect.");
        Expect.True(menu.MenuText.Contains("FIN +1 Accuracy and +1 Evasion"), "Main menu body should explain Finesse's exact training effect.");
    }

    private static void CharacterCreationStarterKitUsesContentDescriptions()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);

        var kit = menu.BuildStarterKitPreviewText();
        Expect.True(kit.Contains("Equipped:"), "Starter kit preview should split equipped items into their own section.");
        Expect.True(kit.Contains("Pack:"), "Starter kit preview should split carried items into their own section.");
        Expect.True(kit.Contains("Iron Sword"), "Starter kit preview should use the content display name for sword_iron.");
        Expect.True(kit.Contains("Wooden Shield"), "Starter kit preview should use the content display name for shield_wooden.");
        Expect.True(kit.Contains("Health Potion"), "Starter kit preview should use the content display name for potion_health.");
        Expect.True(kit.Contains("Reliable melee weapon") || kit.Contains("Restores health"), "Starter kit preview should include concise item descriptions.");
        Expect.False(kit.Contains("sword_iron"), "Starter kit preview should not expose raw item ids when content is available.");
    }

    private static void CharacterCreationMysticPreviewNotesTargetedScrolls()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);
        menu.HandleKey(Key.Down);  // Name
        menu.HandleKey(Key.Down);  // Archetype
        menu.HandleKey(Key.Right); // Skirmisher
        menu.HandleKey(Key.Right); // Mystic

        var kit = menu.BuildStarterKitPreviewText();
        Expect.True(kit.Contains("Scroll of Fireball"), "Mystic starter kit should display the fireball scroll by content display name.");
        Expect.True(kit.Contains("Scroll of Blink"), "Mystic starter kit should display the blink scroll by content display name.");
        Expect.True(kit.Contains("Requires targeting"), "Targeted starter-kit scrolls should advertise that they require targeting.");
    }

    private static void MainMenuTabShowsStarterKitTooltip()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);
        menu._Ready();
        menu.Open();

        Expect.True(menu.HandleKey(Key.Tab), "Main menu should handle Tab for the starter-kit tooltip.");
        var tooltip = FindChild<Tooltip>(menu, "Tooltip");

        Expect.NotNull(tooltip, "Tab should create a starter-kit tooltip on the main menu.");
        Expect.True(tooltip!.Visible, "Starter-kit tooltip should be visible after pressing Tab.");
        Expect.Equal("Starter Kit", tooltip.TitleText, "Starter-kit tooltip should use the expected title.");
        Expect.True(tooltip.BodyText.Contains("Equipped items start worn. Pack items are carried supplies."), "Starter-kit tooltip should explain Equipped and Pack sections.");
        Expect.True(tooltip.BodyText.Contains("Equipped:"), "Starter-kit tooltip should include the Equipped section.");
        Expect.True(tooltip.BodyText.Contains("Pack:"), "Starter-kit tooltip should include the Pack section.");
        Expect.True(tooltip.BodyText.Contains("Health Potion"), "Starter-kit tooltip should include content-resolved potion names.");
        Expect.True(tooltip.BodyText.Contains("Iron Sword"), "Starter-kit tooltip should include content-resolved weapon names.");
    }

    private static void MainMenuMysticStarterKitTooltipNotesTargeting()
    {
        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        var menu = new MainMenu();
        menu.Bind(gameManager, bus);
        menu._Ready();
        menu.Open();
        menu.HandleKey(Key.Down);  // Name
        menu.HandleKey(Key.Down);  // Archetype
        menu.HandleKey(Key.Right); // Skirmisher
        menu.HandleKey(Key.Right); // Mystic

        Expect.True(menu.HandleKey(Key.Tab), "Main menu should handle Tab for the Mystic starter-kit tooltip.");
        var tooltip = FindChild<Tooltip>(menu, "Tooltip");

        Expect.NotNull(tooltip, "Tab should create a Mystic starter-kit tooltip on the main menu.");
        Expect.True(tooltip!.BodyText.Contains("Scroll of Fireball"), "Mystic starter-kit tooltip should include the fireball scroll.");
        Expect.True(tooltip.BodyText.Contains("Scroll of Blink"), "Mystic starter-kit tooltip should include the blink scroll.");
        Expect.True(tooltip.BodyText.Contains("Requires targeting"), "Mystic starter-kit tooltip should include targeting notes.");
    }

    private static void LevelUpSpendReducesPointsAndIncreasesStat()
    {
        var context = CreateContext();
        var player = context.Player;
        var progression = new ProgressionComponent
        {
            Level = 2,
            Experience = 60,
            ExperienceToNextLevel = 150,
            UnspentStatPoints = 3,
        };
        player.SetComponent(progression);

        var sheet = new CharacterSheet();
        sheet.Bind(context.GameManager, context.Bus, context.Content);
        sheet.Open();

        var originalAttack = player.Stats.Attack;

        // Navigate to Attack (index 1) and spend
        sheet.HandleKey(Key.Down); // -> Attack
        sheet.HandleKey(Key.Enter);

        Expect.Equal(2, progression.UnspentStatPoints, "Spending a stat point should reduce UnspentStatPoints.");
        Expect.Equal(originalAttack + 1, player.Stats.Attack, "Spending on Attack should increase Attack by 1.");

        // Spend on MaxHP (index 0)
        sheet.HandleKey(Key.Up); // -> MaxHP
        var originalMaxHp = player.Stats.MaxHP;
        sheet.HandleKey(Key.Enter);

        Expect.Equal(1, progression.UnspentStatPoints, "Spending another point should reduce to 1.");
        Expect.Equal(originalMaxHp + 3, player.Stats.MaxHP, "Spending on MaxHP should increase MaxHP by 3.");
    }

    private static void LevelUpUIShowsPrompt()
    {
        var context = CreateContext();
        var player = context.Player;
        var progression = new ProgressionComponent { Level = 2, UnspentStatPoints = 2 };
        player.SetComponent(progression);

        var sheet = new CharacterSheet();
        sheet.Bind(context.GameManager, context.Bus, context.Content);
        sheet.Open();

        Expect.True(sheet.SummaryText.Contains("TRAINING:"), "Character sheet should show the training prompt when points are available.");
        Expect.True(sheet.SummaryText.Contains("2 point(s) available"), "Character sheet should show how many points are available.");
        Expect.True(sheet.SummaryText.Contains("MaxHP"), "Character sheet should list MaxHP as a spendable stat.");
        Expect.True(sheet.SummaryText.Contains("Attack"), "Character sheet should list Attack as a spendable stat.");
    }

    private static void LevelUpOverlayListsUnlockedPerks()
    {
        var context = CreateContext();
        context.Player.SetComponent(new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 });

        var overlay = new LevelUpOverlay();
        overlay.Bind(context.GameManager);
        overlay.Open();

        Expect.True(overlay.SummaryText.Contains("Battle Instinct"), "Overlay should list unlocked perk names.");
        Expect.True(overlay.SummaryText.Contains("Quartermaster's Eye"), "Overlay should list all unlocked level 2 perks.");
    }

    private static void LevelUpOverlayAppliesSelectedPerk()
    {
        var context = CreateContext();
        context.Player.SetComponent(new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 });

        var overlay = new LevelUpOverlay();
        overlay.Bind(context.GameManager);
        overlay.Open();
        overlay.HandleKey(Key.Enter);

        var progression = context.Player.GetComponent<ProgressionComponent>()!;
        Expect.Equal(0, progression.UnspentPerkChoices, "Choosing a perk should consume the pending perk choice.");
        Expect.True(progression.SelectedPerkIds.Contains("battle_instinct"), "Choosing the default overlay selection should record the perk.");
        Expect.Equal(9, context.Player.Stats.Attack, "Choosing Battle Instinct should increase attack.");
    }

    private static void LevelUpOverlayUsesCompactRightRailLayout()
    {
        var context = CreateContext();
        context.Player.SetComponent(new ProgressionComponent { Level = 2, UnspentPerkChoices = 1 });

        var overlay = new LevelUpOverlay();
        overlay.Bind(context.GameManager);
        overlay.Open();

        Expect.True(overlay.SummaryText.Contains("LEVEL UP"), "Overlay should use a stronger title header.");
        Expect.True(overlay.SummaryText.Contains("Available Perks"), "Overlay should separate the available perks list into its own section.");
        Expect.True(overlay.SummaryText.Contains("Selected Perk"), "Overlay should separate the selected perk details into their own section.");
        Expect.True(overlay.Children.Count > 0 && overlay.Children[0] is Panel, "Overlay should create a panel for the level-up card.");

        var panel = (Panel)overlay.Children[0];
        Expect.True(panel.Position.X > 600f,
            "Overlay panel should sit on the right side of the gameplay viewport instead of blocking the center.");
        Expect.True(panel.Position.Y < 120f,
            "Overlay panel should sit near the top of the viewport so the center play space stays clearer.");
    }

    private static void EquipmentComparisonGeneratesCorrectDelta()
    {
        var newMods = new Dictionary<string, int> { ["attack"] = 3, ["defense"] = 1 };
        var equippedMods = new Dictionary<string, int> { ["attack"] = 1, ["speed"] = 10 };

        var result = InventoryUI.BuildEquipmentComparisonText(newMods, equippedMods);

        Expect.True(result.Contains("vs equipped:"), "Comparison should include the 'vs equipped:' prefix.");
        Expect.True(result.Contains("attack: +2"), "Comparison should show attack delta as +2.");
        Expect.True(result.Contains("defense: +1"), "Comparison should show defense delta as +1.");
        Expect.True(result.Contains("speed: -10"), "Comparison should show speed delta as -10.");
    }

    private static void EquipmentComparisonSameStats()
    {
        var mods = new Dictionary<string, int> { ["attack"] = 2 };
        var result = InventoryUI.BuildEquipmentComparisonText(mods, mods);

        Expect.True(result.Contains("same stats"), "Identical stat modifiers should report 'same stats'.");
    }

    private static void HudShowsLevelUpIndicator()
    {
        var context = CreateContext();
        var hud = new HUD();
        hud.Bind(context.GameManager, context.Bus);

        var progression = new ProgressionComponent { Level = 2, UnspentStatPoints = 1, Experience = 60, ExperienceToNextLevel = 150 };
        context.Player.SetComponent(progression);

        context.Bus.EmitTurnCompleted();

        Expect.True(hud.LevelText.Contains("LV UP!"), "HUD should show LV UP! when unspent stat points are available.");

        progression.UnspentStatPoints = 0;
        context.Bus.EmitTurnCompleted();

        Expect.False(hud.LevelText.Contains("LV UP!"), "HUD should not show LV UP! when no unspent stat points remain.");
    }

    private static void HelpOverlayDocumentsNewFeatures()
    {
        var overlay = new HelpOverlay();

        overlay.OpenMainMenuHelp();
        Expect.True(overlay.CurrentBodyText.Contains("Stat Preview"), "Main menu help should mention Stat Preview.");
        Expect.True(overlay.CurrentBodyText.Contains("Training"), "Main menu help should mention Training points.");
        Expect.True(overlay.CurrentBodyText.Contains("+3 Max HP"), "Main menu help should document exact training stat effects.");
        Expect.True(overlay.CurrentBodyText.Contains("Equipped"), "Main menu help should describe equipped starter-kit items.");
        Expect.True(overlay.CurrentBodyText.Contains("Pack"), "Main menu help should describe pack starter-kit items.");
        Expect.True(overlay.CurrentBodyText.Contains("targeting"), "Main menu help should mention aimed scroll targeting.");

        overlay.OpenGameplayHelp();
        Expect.True(overlay.CurrentBodyText.Contains("Level Up"), "Gameplay help should mention Level Up.");
        Expect.True(overlay.CurrentBodyText.Contains("Equipment comparison"), "Gameplay help should mention equipment comparison.");
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

    private static T? FindChild<T>(Node parent, string name) where T : Node
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is T typed && typed.Name == name)
            {
                return typed;
            }
        }

        return null;
    }
}
