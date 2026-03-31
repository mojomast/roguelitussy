using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public static class WorldArtCatalog
{
    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private const string TileBasePath = "res://Assets/Tilesets/0x72/";
    private const string SpriteBasePath = "res://Assets/Sprites/0x72/";

    public static Texture2D? GetTileTexture(TileType tileType, bool isDoorOpen)
    {
        return tileType switch
        {
            TileType.Floor => Load(TileBasePath + "Floor_Clean.png"),
            TileType.Wall => Load(TileBasePath + "Wall_Mid.png"),
            TileType.Door when isDoorOpen => Load(TileBasePath + "Door_Open.png"),
            TileType.Door => Load(TileBasePath + "Door_Closed.png"),
            TileType.StairsUp => Load(TileBasePath + "Floor_Ladder.png"),
            TileType.StairsDown => Load(TileBasePath + "Floor_Ladder.png"),
            _ => null,
        };
    }

    public static string? GetTileMarker(TileType tileType, bool isDoorOpen)
    {
        return tileType switch
        {
            TileType.StairsUp => "UP",
            TileType.StairsDown => "DN",
            TileType.Door when isDoorOpen => "//",
            TileType.Door => "[]",
            _ => null,
        };
    }

    public static Texture2D? GetEntityTexture(IEntity entity)
    {
        return entity.Faction switch
        {
            Faction.Player => PlayerVisualCatalog.GetBaseTexture(entity),
            Faction.Enemy => Load(SpriteBasePath + "Goblin_Idle_1.png"),
            _ => null,
        };
    }

    private static Texture2D? Load(string path)
    {
        if (!TextureCache.TryGetValue(path, out var texture))
        {
            texture = GD.Load<Texture2D>(path);
            TextureCache[path] = texture;
        }

        return texture;
    }
}