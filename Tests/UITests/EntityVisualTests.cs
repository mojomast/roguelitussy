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
        registry.Add("UI.EntityRenderer applies identity-driven player sprite tint without world overlays", EntityRendererAppliesIdentityDrivenPlayerSpriteTintWithoutWorldOverlays);
        registry.Add("UI.EntityRenderer refreshes player sprite variant when identity changes", EntityRendererRefreshesPlayerSpriteVariant);
        registry.Add("UI.EntityRenderer uses different 0x72 portraits for different builds", EntityRendererUsesDifferent0x72Portraits);
        registry.Add("UI.EntityRenderer resolves default vanguard portrait from variant", EntityRendererResolvesDefaultVanguardPortraitFromVariant);
        registry.Add("UI.EntityRenderer falls back when portrait import cache is missing", EntityRendererFallsBackWhenPortraitImportCacheIsMissing);
        registry.Add("UI.EntityRenderer keeps visible bodies across repeated refreshes", EntityRendererKeepsVisibleBodiesAcrossRepeatedRefreshes);
        registry.Add("UI.EntityRenderer crops tall player portraits to gameplay bounds", EntityRendererCropsTallPlayerPortraitsToGameplayBounds);
        registry.Add("UI.EntityRenderer honors bound world visibility during stale cache updates", EntityRendererHonorsBoundWorldVisibilityDuringStaleCacheUpdates);
        registry.Add("UI.EntityRenderer gives chests dedicated non-humanoid visuals", EntityRendererGivesChestsDedicatedVisuals);
    }

    private static void EntityRendererAppliesIdentityDrivenPlayerSpriteTintWithoutWorldOverlays()
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

        Expect.NotNull(body, "Player renderer should keep the base body sprite.");
        Expect.Equal(0.90f, body!.Modulate.G, "Elf variants should tint the body sprite with the elf palette.");
        Expect.True(FindChild<ColorRect>(spriteRoot!, "AccentBand") is null,
            "Gameplay sprites should not add a world-space accent band that reads like a clipping artifact.");
        Expect.True(FindChild<Label>(spriteRoot!, "VariantSigil") is null,
            "Gameplay sprites should not add a race sigil above the actor in world space.");
        Expect.True(FindChild<Label>(spriteRoot!, "VariantDetail") is null,
            "Gameplay sprites should not add appearance marker labels into the world render.");
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
        var initialBody = FindChild<Sprite2D>(spriteRoot, "Body")!;
        var initialTexture = (Texture2D)initialBody.Texture!;
        var initialModulate = initialBody.Modulate;

        player.SetComponent(new IdentityComponent
        {
            RaceId = "elf",
            GenderId = "feminine",
            AppearanceId = "weathered",
            SpriteVariantId = "elf_feminine_weathered_skirmisher",
        });

        renderer.UpsertEntity(player);

        var updatedBody = FindChild<Sprite2D>(spriteRoot, "Body")!;
        var updatedTexture = (Texture2D)updatedBody.Texture!;
        var updatedModulate = updatedBody.Modulate;

        Expect.False(GetTextureSourcePath(initialTexture) == GetTextureSourcePath(updatedTexture),
            "Updating the identity component should refresh the base portrait selection.");
        Expect.True(initialModulate.R != updatedModulate.R || initialModulate.G != updatedModulate.G || initialModulate.B != updatedModulate.B,
            "Updating the identity component should refresh the body tint even without extra world overlays.");
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
        Expect.False(GetTextureSourcePath(vanguardSprite) == GetTextureSourcePath(skirmisherSprite),
            "Different builds should no longer share the same base portrait file.");
    }

    private static void EntityRendererResolvesDefaultVanguardPortraitFromVariant()
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

        var body = FindChild<Sprite2D>(renderer.GetSprite(player.Id)!, "Body");
        Expect.NotNull(body, "Default vanguard player should render a sprite body.");
        var texture = body!.Texture;
        Expect.NotNull(texture, "Default vanguard player should keep a loaded texture assigned.");
        Expect.True(texture!.ResourcePath.EndsWith("Knight_Male_Idle_1.png", System.StringComparison.Ordinal),
            "The archetype encoded in SpriteVariantId should resolve the default human vanguard to the knight portrait.");
    }

    private static void EntityRendererFallsBackWhenPortraitImportCacheIsMissing()
    {
        const string path = "res://Assets/Sprites/0x72/Orc_Warrior_Idle_1.png";
        WorldArtCatalog.ClearTextureCachesForTests();
        GD.MissingResourcePaths.Add(path);
        try
        {
            var renderer = new EntityRenderer(new Node2D(), new AnimationController());
            var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
            player.SetComponent(new IdentityComponent
            {
                RaceId = "orc",
                GenderId = "masculine",
                AppearanceId = "default",
                SpriteVariantId = "orc_masculine_default_skirmisher",
            });

            renderer.UpsertEntity(player);

            var body = FindChild<Sprite2D>(renderer.GetSprite(player.Id)!, "Body");
            Expect.NotNull(body, "Player should still create a sprite body when imported portrait cache files are absent.");
            Expect.True(body!.Texture is ImageTexture,
                "Player portraits should fall back to source image loading when the generated Godot import cache is missing on first run.");
            Expect.Equal(path, body.Texture!.ResourcePath,
                "Fallback player portrait should preserve the source resource path.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(path);
            WorldArtCatalog.ClearTextureCachesForTests();
        }
    }

    private static void EntityRendererKeepsVisibleBodiesAcrossRepeatedRefreshes()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new IdentityComponent
        {
            RaceId = "human",
            GenderId = "neutral",
            AppearanceId = "weathered",
            SpriteVariantId = "human_neutral_weathered_vanguard",
        });

        var enemy = new StubEntity("Goblin Archer", new Position(2, 1), Faction.Enemy);

        renderer.UpsertEntity(player);
        renderer.UpsertEntity(enemy);
        renderer.UpsertEntity(player);
        renderer.UpsertEntity(enemy);

        var playerSprite = renderer.GetSprite(player.Id)!;
        var enemySprite = renderer.GetSprite(enemy.Id)!;
        var playerBody = FindChild<Sprite2D>(playerSprite, "Body");
        var enemyBody = FindChild<Sprite2D>(enemySprite, "Body");

        Expect.True(playerSprite.Visible, "Repeated refreshes should not hide the player sprite root.");
        Expect.True(enemySprite.Visible, "Repeated refreshes should not hide enemy sprite roots.");
        Expect.NotNull(playerBody, "Repeated refreshes should preserve the player body sprite node.");
        Expect.NotNull(enemyBody, "Repeated refreshes should preserve enemy body sprite nodes.");
        Expect.NotNull(playerBody!.Texture, "Repeated refreshes should not drop the player texture.");
        Expect.NotNull(enemyBody!.Texture, "Repeated refreshes should not drop the enemy texture.");
    }

    private static void EntityRendererCropsTallPlayerPortraitsToGameplayBounds()
    {
        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new IdentityComponent
        {
            RaceId = "human",
            GenderId = "neutral",
            AppearanceId = "default",
            SpriteVariantId = "human_neutral_default_mystic",
        });

        renderer.UpsertEntity(player);

        var spriteRoot = renderer.GetSprite(player.Id)!;
        var body = FindChild<Sprite2D>(spriteRoot, "Body");

        Expect.NotNull(body, "Player renderer should create a body sprite for tall portraits.");
        Expect.True(body!.Texture is Texture2D,
            "Tall player portraits should keep the imported texture assigned to the sprite.");
        Expect.True(body.RegionEnabled,
            "Tall player portraits should crop their transparent headroom with Sprite2D region rendering instead of runtime texture wrappers.");
        Expect.Equal(8f, body.RegionRect.Position.Y,
            "Tall player portraits should skip the empty top rows that were pushing the sprite into nearby wall space.");
        Expect.Equal(20f, body.RegionRect.Size.Y,
            "Tall player portraits should keep only the gameplay-visible body region after cropping.");
        Expect.Equal(6f, body.Position.Y,
            "Cropped tall portraits should sit lower in the tile so the sprite clears the north wall lip in gameplay.");
        Expect.Equal(1.4f, body.Scale.Y,
            "Cropped tall portraits should scale to the tighter gameplay height budget so they no longer clip into the top wall.");
    }

    private static string GetTextureSourcePath(Texture2D texture)
    {
        return texture.ResourcePath;
    }

    private static void EntityRendererHonorsBoundWorldVisibilityDuringStaleCacheUpdates()
    {
        var world = new WorldState();
        world.InitGrid(5, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new IdentityComponent
        {
            RaceId = "human",
            GenderId = "neutral",
            AppearanceId = "default",
            SpriteVariantId = "human_neutral_default_vanguard",
        });

        var enemy = new StubEntity("Goblin Archer", new Position(2, 1), Faction.Enemy);
        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        world.SetVisible(player.Position, true);
        world.SetVisible(enemy.Position, true);

        var renderer = new EntityRenderer(new Node2D(), new AnimationController());
        renderer.BindWorld(world);
        renderer.UpdateVisibility(System.Array.Empty<Position>());

        Expect.True(renderer.GetSprite(player.Id)!.Visible,
            "Player visibility should respect the bound world state even if the renderer cache is temporarily empty.");
        Expect.True(renderer.GetSprite(enemy.Id)!.Visible,
            "Enemy visibility should respect the bound world state even if the renderer cache is temporarily empty.");
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
