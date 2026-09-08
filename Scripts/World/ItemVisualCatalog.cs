using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public static class ItemVisualCatalog
{
    private static readonly Dictionary<string, Texture2D?> TextureCache = new();

    public static Texture2D? GetTexture(ItemInstance item, IContentDatabase? content)
    {
        if (content?.TryGetItemTemplate(item.TemplateId, out var template) != true)
        {
            return null;
        }

        return GetTexture(template);
    }

    public static Texture2D? GetTexture(ItemTemplate template)
    {
        return string.IsNullOrWhiteSpace(template.SpritePath)
            ? null
            : RuntimeTextureLoader.Load(template.SpritePath, TextureCache);
    }

    public static void ClearTextureCacheForTests()
    {
        TextureCache.Clear();
    }
}
