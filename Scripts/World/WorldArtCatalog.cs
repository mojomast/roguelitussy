using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

internal static class WorldArtCatalog
{
    private static readonly Dictionary<string, Texture2D?> TextureCache = new();

    public static Texture2D? GetTileTexture(TileType tileType, bool isDoorOpen)
    {
        return tileType switch
        {
            TileType.Floor => Load("res://Assets/Tilesets/kenney_floor.png"),
            TileType.Wall => Load("res://Assets/Tilesets/kenney_wall.png"),
            TileType.Door when isDoorOpen => Load("res://Assets/Tilesets/kenney_floor.png"),
            TileType.Door => Load("res://Assets/Tilesets/kenney_door_closed.png"),
            TileType.StairsUp => Load("res://Assets/Tilesets/kenney_stairs_up.png"),
            TileType.StairsDown => Load("res://Assets/Tilesets/kenney_stairs_down.png"),
            _ => null,
        };
    }

    public static Texture2D? GetEntityTexture(IEntity entity)
    {
        return entity.Faction switch
        {
            Faction.Player => Load("res://Assets/Sprites/player_tiny_dungeon.png"),
            Faction.Enemy => Load("res://Assets/Sprites/enemies/enemy_tiny_dungeon.png"),
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