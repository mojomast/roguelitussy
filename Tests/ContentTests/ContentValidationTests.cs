using System.IO;
using Xunit;
using Roguelike.Core.Content;

namespace Roguelike.Tests;

public class ContentValidationTests
{
    private static string GetContentPath()
    {
        // Navigate from test bin directory up to repo root
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "project.godot")))
            dir = Directory.GetParent(dir)?.FullName;
        return Path.Combine(dir!, "Content");
    }

    [Fact]
    public void LoadAll_LoadsItemsFromJson()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        Assert.NotEmpty(loader.AllItems);
    }

    [Fact]
    public void LoadAll_LoadsEnemiesFromJson()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        Assert.NotEmpty(loader.AllEnemies);
    }

    [Fact]
    public void GetItem_ReturnsKnownItem()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        var sword = loader.GetItem("sword_iron");
        Assert.NotNull(sword);
        Assert.Equal("Iron Sword", sword.DisplayName);
        Assert.True(sword.StatModifiers.ContainsKey("Attack"));
    }

    [Fact]
    public void GetEnemy_ReturnsKnownEnemy()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        var goblin = loader.GetEnemy("goblin");
        Assert.NotNull(goblin);
        Assert.Equal("Goblin", goblin.DisplayName);
        Assert.True(goblin.BaseStats.HP > 0);
    }

    [Fact]
    public void GetEnemiesForDepth_FiltersCorrectly()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        var depth0 = loader.GetEnemiesForDepth(0);
        Assert.NotEmpty(depth0);

        // All returned enemies should have minDepth <= 0
        foreach (var enemy in depth0)
            Assert.True(enemy.MinDepth <= 0, $"{enemy.TemplateId} has minDepth {enemy.MinDepth}");
    }

    [Fact]
    public void GetItem_UnknownId_ReturnsNull()
    {
        var loader = new ContentLoader(GetContentPath());
        loader.LoadAll();

        Assert.Null(loader.GetItem("nonexistent_item"));
    }
}
