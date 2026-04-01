using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class EntityVisualTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.EntityRenderer applies identity-driven player sprite overlays", EntityRendererAppliesIdentityDrivenPlayerSpriteOverlays);
        registry.Add("UI.EntityRenderer refreshes player sprite variant when identity changes", EntityRendererRefreshesPlayerSpriteVariant);
        registry.Add("UI.EntityRenderer uses different 0x72 portraits for different builds", EntityRendererUsesDifferent0x72Portraits);
        registry.Add("UI.EntityRenderer gives chests dedicated non-humanoid visuals", EntityRendererGivesChestsDedicatedVisuals);
    }

    private static void EntityRendererAppliesIdentityDrivenPlayerSpriteOverlays()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new IdentityComponent
        {
            RaceId = "elf",
            GenderId = "feminine",
            AppearanceId = "scarred",
            SpriteVariantId = "elf_feminine_scarred_skirmisher",
        });

        renderer.UpsertEntity(player);

        var spriteRoot = renderer.GetSprite(player.Id);
        Expect.NotNull(spriteRoot, "Player renderer should create a sprite root.");

        var body = FindChild<Sprite2D>(spriteRoot!, "Body");
        var accentBand = FindChild<ColorRect>(spriteRoot!, "AccentBand");
        var variantSigil = FindChild<Label>(spriteRoot!, "VariantSigil");
        var variantDetail = FindChild<Label>(spriteRoot!, "VariantDetail");

        Expect.NotNull(body, "Player renderer should keep the base body sprite.");
        Expect.NotNull(accentBand, "Player renderer should add an accent band for identity variants.");
        Expect.NotNull(variantSigil, "Player renderer should add a race sigil overlay.");
        Expect.NotNull(variantDetail, "Player renderer should add an appearance overlay.");
        Expect.Equal("/", variantSigil!.Text, "Elf variants should render the elf race sigil overlay.");
        Expect.Equal("!", variantDetail!.Text, "Scarred variants should render the scarred appearance overlay.");
        Expect.Equal(0.90f, body!.Modulate.G, "Elf variants should tint the body sprite with the elf palette.");
        Expect.Equal(0.64f, accentBand!.Color.B, "Feminine variants should tint the accent band with the feminine palette.");
    }

    private static void EntityRendererRefreshesPlayerSpriteVariant()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new IdentityComponent
        {
            RaceId = "human",
            GenderId = "neutral",
            AppearanceId = "default",
            SpriteVariantId = "human_neutral_default_vanguard",
        });

        renderer.UpsertEntity(player);

        var spriteRoot = renderer.GetSprite(player.Id)!;
        var initialSigil = FindChild<Label>(spriteRoot, "VariantSigil")!.Text;
        var initialDetail = FindChild<Label>(spriteRoot, "VariantDetail")!.Text;
        var initialAccent = FindChild<ColorRect>(spriteRoot, "AccentBand")!.Color;

        player.SetComponent(new IdentityComponent
        {
            RaceId = "dwarf",
            GenderId = "masculine",
            AppearanceId = "weathered",
            SpriteVariantId = "dwarf_masculine_weathered_vanguard",
        });

        renderer.UpsertEntity(player);

        var updatedSigil = FindChild<Label>(spriteRoot, "VariantSigil")!.Text;
        var updatedDetail = FindChild<Label>(spriteRoot, "VariantDetail")!.Text;
        var updatedAccent = FindChild<ColorRect>(spriteRoot, "AccentBand")!.Color;

        Expect.False(initialSigil == updatedSigil, "Updating the identity component should refresh the race sigil overlay.");
        Expect.False(initialDetail == updatedDetail, "Updating the identity component should refresh the appearance overlay.");
        Expect.True(initialAccent.R != updatedAccent.R || initialAccent.G != updatedAccent.G || initialAccent.B != updatedAccent.B,
            "Updating the identity component should refresh the accent color.");
    }

    private static void EntityRendererUsesDifferent0x72Portraits()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());

        var vanguard = new StubEntity("Vanguard", new Position(1, 1), Faction.Player);
        vanguard.SetComponent(new IdentityComponent
        {
            RaceId = "dwarf",
            GenderId = "masculine",
            AppearanceId = "default",
            SpriteVariantId = "dwarf_masculine_default_vanguard",
        });

        var skirmisher = new StubEntity("Skirmisher", new Position(2, 1), Faction.Player);
        skirmisher.SetComponent(new IdentityComponent
        {
            RaceId = "elf",
            GenderId = "feminine",
            AppearanceId = "default",
            SpriteVariantId = "elf_feminine_default_skirmisher",
        });

        renderer.UpsertEntity(vanguard);
        renderer.UpsertEntity(skirmisher);

        var vanguardTexture = FindChild<Sprite2D>(renderer.GetSprite(vanguard.Id)!, "Body")!.Texture;
        var skirmisherTexture = FindChild<Sprite2D>(renderer.GetSprite(skirmisher.Id)!, "Body")!.Texture;

        Expect.True(vanguardTexture is Texture2D, "Player portraits should resolve to imported 0x72 textures.");
        Expect.True(skirmisherTexture is Texture2D, "Player portraits should resolve to imported 0x72 textures.");

        var vanguardSprite = (Texture2D)vanguardTexture!;
        var skirmisherSprite = (Texture2D)skirmisherTexture!;
        Expect.False(vanguardSprite.ResourcePath == skirmisherSprite.ResourcePath,
            "Different builds should no longer share the same base portrait file.");
    }

    private static void EntityRendererGivesChestsDedicatedVisuals()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        var chest = new StubEntity("Treasure Chest", new Position(3, 3), Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "deep_floor_loot" });

        renderer.UpsertEntity(chest);

        var spriteRoot = renderer.GetSprite(chest.Id)!;
        var body = FindChild<ColorRect>(spriteRoot, "Body");
        var chestBand = FindChild<ColorRect>(spriteRoot, "ChestBand");
        var chestLatch = FindChild<Label>(spriteRoot, "ChestLatch");

        Expect.NotNull(body, "Chest rendering should use a dedicated fallback body instead of a humanoid sprite texture.");
        Expect.NotNull(chestBand, "Chest rendering should add a distinct chest band overlay.");
        Expect.NotNull(chestLatch, "Chest rendering should add a distinct latch marker.");
        Expect.Equal("C", chestLatch!.Text, "Chest latch marker should stay readable in the fallback presentation.");
        Expect.True(FindChild<ColorRect>(spriteRoot, "AccentBand") is null, "Chest visuals should not reuse player accent overlays.");
        Expect.True(FindChild<Label>(spriteRoot, "VariantSigil") is null, "Chest visuals should not reuse player identity sigils.");
    }

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