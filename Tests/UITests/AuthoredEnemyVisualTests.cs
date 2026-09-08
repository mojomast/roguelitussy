using System.Collections.Generic;
using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class AuthoredEnemyVisualTests : ITestSuite
{
    private const string RatPath = "res://Assets/Sprites/0x72/Imp_Idle_1.png";
    private const string AuthoredPath = "res://Assets/Sprites/0x72/Zombie_Idle_1.png";

    public void Register(TestRegistry registry)
    {
        registry.Add("UI.AuthoredEnemyVisual renders every authored template through world content", RendersEveryAuthoredTemplate);
        registry.Add("UI.AuthoredEnemyVisual identity overrides name and refresh preserves body nodes", IdentityOverridesNameAndRefreshPreservesBody);
        registry.Add("UI.AuthoredEnemyVisual missing content identity and paths retain fallbacks", MissingMetadataRetainsFallbacks);
        registry.Add("UI.AuthoredEnemyVisual missing import uses authored source texture", MissingImportUsesSourceTexture);
        registry.Add("UI.AuthoredEnemyVisual unloadable import and source retain fallbacks", UnloadableTextureRetainsFallbacks);
        registry.Add("UI.AuthoredEnemyVisual world content overrides service without leaking across binds", ContentBindingUsesWorldThenService);
        registry.Add("UI.AuthoredEnemyVisual preserves player neutral and chest appearance", PreservesNonEnemyAppearance);
    }

    private static void RendersEveryAuthoredTemplate()
    {
        WorldArtCatalog.ClearTextureCachesForTests();
        var content = ContentLoader.LoadFromRepository();
        var world = new WorldState { ContentDatabase = content };
        world.InitGrid(content.EnemyDefinitions.Count, 1);
        var index = 0;
        foreach (var definition in content.EnemyDefinitions.Values)
        {
            var template = content.EnemyTemplates[definition.Id];
            Expect.Equal(definition.SpritePath, template.SpritePath, $"'{definition.Id}' must project authored sprite metadata.");
            var position = new Position(index++, 0);
            world.SetTile(position, TileType.Floor);
            var enemy = new StubEntity(definition.Name, position, Faction.Enemy);
            enemy.SetComponent(new EnemyComponent { TemplateId = definition.Id });
            world.AddEntity(enemy);
        }

        var renderer = new EntityRenderer();
        renderer.BindWorld(world);

        Expect.True(world.Entities.Count > 0, "The content-backed roster must not be empty.");
        Expect.Equal(content.EnemyDefinitions.Count, renderer.SpriteCount, "Every authored enemy must have a rendered root.");
        foreach (var enemy in world.Entities)
        {
            var id = enemy.GetComponent<EnemyComponent>()!.TemplateId;
            Expect.Equal(content.EnemyDefinitions[id].SpritePath, TexturePath(renderer, enemy), $"'{id}' must render its authored texture.");
        }
    }

    private static void IdentityOverridesNameAndRefreshPreservesBody()
    {
        var content = ContentLoader.LoadFromRepository();
        var renderer = new EntityRenderer(content: content);
        var enemy = Enemy("Giant Rat", "dark_mage");
        renderer.UpsertEntity(enemy);
        var root = renderer.GetSprite(enemy.Id)!;
        var body = Body(renderer, enemy);
        var texture = ((Sprite2D)body).Texture;
        var childCount = root.GetChildren().Count;
        Expect.Equal(AuthoredPath, TexturePath(renderer, enemy), "Template identity must override a conflicting mapped display name.");

        for (var i = 0; i < 100; i++)
        {
            renderer.UpsertEntity(enemy);
        }

        Expect.True(ReferenceEquals(root, renderer.GetSprite(enemy.Id)), "Refresh must preserve the root node.");
        Expect.True(ReferenceEquals(body, Body(renderer, enemy)), "Refresh must preserve the body node.");
        Expect.True(ReferenceEquals(texture, ((Sprite2D)body).Texture), "Refresh must reuse the cached texture.");
        Expect.Equal(childCount, root.GetChildren().Count, "Refresh must not accumulate visual children.");
    }

    private static void MissingMetadataRetainsFallbacks()
    {
        var content = new StubContentDatabase();
        var templates = (Dictionary<string, EnemyTemplate>)content.EnemyTemplates;
        templates["goblin"] = templates["goblin"] with { SpritePath = " " };
        var cases = new (IContentDatabase? Content, string? TemplateId)[]
        {
            (null, "goblin"),
            (content, null),
            (content, ""),
            (content, "missing_template"),
            (content, "goblin"),
        };
        foreach (var (database, templateId) in cases)
        {
            var renderer = new EntityRenderer(content: database);
            var mapped = Enemy("Giant Rat", templateId);
            renderer.UpsertEntity(mapped);
            Expect.Equal(RatPath, TexturePath(renderer, mapped), "Missing metadata must preserve name-based textures.");

            var unmapped = Enemy("Unmapped Creature", templateId);
            renderer.UpsertEntity(unmapped);
            Expect.True(Body(renderer, unmapped) is ColorRect, "Missing metadata and an unknown name must preserve the procedural body.");
        }
    }

    private static void MissingImportUsesSourceTexture()
    {
        WorldArtCatalog.ClearTextureCachesForTests();
        GD.MissingResourcePaths.Add(AuthoredPath);
        try
        {
            var renderer = new EntityRenderer(content: ContentLoader.LoadFromRepository());
            var enemy = Enemy("Giant Rat", "dark_mage");
            renderer.UpsertEntity(enemy);
            Expect.Equal(AuthoredPath, TexturePath(renderer, enemy), "Missing import must still use the authored source, not the name fallback.");
            Expect.True(((Sprite2D)Body(renderer, enemy)).Texture is ImageTexture, "Missing import must use the existing source-image loader.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(AuthoredPath);
            WorldArtCatalog.ClearTextureCachesForTests();
        }
    }

    private static void UnloadableTextureRetainsFallbacks()
    {
        WorldArtCatalog.ClearTextureCachesForTests();
        var sourcePath = ProjectSettings.GlobalizePath(AuthoredPath);
        GD.MissingResourcePaths.Add(AuthoredPath);
        Image.MissingImagePaths.Add(sourcePath);
        try
        {
            var renderer = new EntityRenderer(content: ContentLoader.LoadFromRepository());
            var mapped = Enemy("Giant Rat", "dark_mage");
            renderer.UpsertEntity(mapped);
            Expect.Equal(RatPath, TexturePath(renderer, mapped), "An unloadable authored asset must fall back to name-based art.");

            var unmapped = Enemy("Unmapped Creature", "dark_mage");
            renderer.UpsertEntity(unmapped);
            var body = Body(renderer, unmapped);
            Expect.True(body is ColorRect, "An unloadable authored asset with an unknown name must produce a procedural body.");
            renderer.UpsertEntity(unmapped);
            Expect.True(ReferenceEquals(body, Body(renderer, unmapped)), "Refreshing a procedural fallback must preserve its body node.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(AuthoredPath);
            Image.MissingImagePaths.Remove(sourcePath);
            WorldArtCatalog.ClearTextureCachesForTests();
        }
    }

    private static void ContentBindingUsesWorldThenService()
    {
        var service = ContentLoader.LoadFromRepository();
        var renderer = new EntityRenderer(content: service);
        var world = new WorldState();
        world.InitGrid(1, 1);
        world.SetTile(new Position(0, 0), TileType.Floor);
        var enemy = new StubEntity("Giant Rat", new Position(0, 0), Faction.Enemy);
        enemy.SetComponent(new EnemyComponent { TemplateId = "dark_mage" });
        world.AddEntity(enemy);

        renderer.BindWorld(world);
        Expect.Equal(AuthoredPath, TexturePath(renderer, enemy), "Binding a content-free world must preserve injected service content.");

        world.ContentDatabase = new StubContentDatabase();
        renderer.BindWorld(world);
        Expect.Equal(RatPath, TexturePath(renderer, enemy), "Bound world content must override injected service content, even for unknown IDs.");

        world.ContentDatabase = null;
        renderer.BindWorld(world);
        Expect.Equal(AuthoredPath, TexturePath(renderer, enemy), "Rebinding must restore the injected service instead of retaining previous world content.");

        var worldOnlyRenderer = new EntityRenderer();
        world.ContentDatabase = service;
        worldOnlyRenderer.BindWorld(world);
        Expect.Equal(AuthoredPath, TexturePath(worldOnlyRenderer, enemy), "Normal world binding must provide authored content without constructor injection.");
        world.ContentDatabase = null;
        worldOnlyRenderer.BindWorld(world);
        Expect.Equal(RatPath, TexturePath(worldOnlyRenderer, enemy), "Content-free rebinding must not retain stale world content.");
    }

    private static void PreservesNonEnemyAppearance()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var faction in new[] { Faction.Player, Faction.Neutral })
        {
            var entity = new StubEntity("Giant Rat", new Position(1, 1), faction);
            entity.SetComponent(new EnemyComponent { TemplateId = "dark_mage" });
            var baseline = new EntityRenderer();
            var authored = new EntityRenderer(content: content);
            baseline.UpsertEntity(entity);
            authored.UpsertEntity(entity);
            Expect.Equal(TexturePath(baseline, entity), TexturePath(authored, entity), "Enemy metadata must not change player or neutral portraits.");
            Expect.Equal(((Sprite2D)Body(baseline, entity)).Modulate, ((Sprite2D)Body(authored, entity)).Modulate, "Enemy metadata must not change portrait tint.");
        }

        var chest = Enemy("Giant Rat", "dark_mage");
        chest.SetComponent(new ChestComponent { LootTableId = "deep_floor_loot" });
        var renderer = new EntityRenderer(content: content);
        renderer.UpsertEntity(chest);
        Expect.True(Body(renderer, chest) is ColorRect, "Chest appearance must take precedence even over enemy identity and faction.");
        var children = renderer.GetSprite(chest.Id)!.GetChildren();
        Expect.True(children.Any(child => child.Name == "ChestBand"), "Chests must keep their dedicated band.");
        Expect.True(children.Any(child => child.Name == "ChestLatch"), "Chests must keep their dedicated latch.");
    }

    private static StubEntity Enemy(string name, string? templateId)
    {
        var enemy = new StubEntity(name, new Position(1, 1), Faction.Enemy);
        if (templateId is not null)
        {
            enemy.SetComponent(new EnemyComponent { TemplateId = templateId });
        }

        return enemy;
    }

    private static Node Body(EntityRenderer renderer, IEntity entity) =>
        renderer.GetSprite(entity.Id)!.GetChildren().Single(child => child.Name == "Body");

    private static string TexturePath(EntityRenderer renderer, IEntity entity)
    {
        var body = Body(renderer, entity) as Sprite2D;
        Expect.NotNull(body, $"'{entity.Name}' must have a sprite body.");
        Expect.NotNull(body!.Texture, $"'{entity.Name}' must have a loaded texture.");
        return body.Texture!.ResourcePath;
    }
}
