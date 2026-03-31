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