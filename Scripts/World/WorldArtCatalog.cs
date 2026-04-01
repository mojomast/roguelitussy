using System;
using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public static class WorldArtCatalog
{
    public readonly record struct TileBoundaryMask(bool North, bool East, bool South, bool West)
    {
        public bool HasAny => North || East || South || West;
    }

    private static readonly Dictionary<string, Texture2D?> TextureCache = new();
    private const string WallBasePath = TileBasePath + "Wall_Mid.png";
    private static readonly string[] FloorVariantPaths =
    {
        TileBasePath + "Floor_Cracks1.png",
        TileBasePath + "Floor_Cracks2.png",
        TileBasePath + "Floor_Cracks3.png",
        TileBasePath + "Floor_Cracks4.png",
        TileBasePath + "Floor_Cracks5.png",
        TileBasePath + "Floor_Cracks6.png",
        TileBasePath + "Floor_Cracks7.png",
    };
    private const string TileBasePath = "res://Assets/Tilesets/0x72/";
    private const string SpriteBasePath = "res://Assets/Sprites/0x72/";

    public static IReadOnlyList<Texture2D> GetTileArtLayers(IWorldState? world, Position position, TileType tileType)
    {
        var layers = new List<Texture2D>(4);
        var isDoorOpen = tileType == TileType.Door && world is WorldState state && state.IsDoorOpen(position);

        switch (tileType)
        {
            case TileType.Floor:
                AppendIfLoaded(layers, ResolveFloorPath(position, allowCracks: true));
                break;
            case TileType.StairsUp:
            case TileType.StairsDown:
                AppendIfLoaded(layers, ResolveFloorPath(position, allowCracks: false));
                AppendIfLoaded(layers, TileBasePath + "Floor_Ladder.png");
                break;
            case TileType.Door:
                AppendIfLoaded(layers, ResolveFloorPath(position, allowCracks: false));
                AppendIfLoaded(layers, TileBasePath + "Door_Frame_Top.png");
                AppendIfLoaded(layers, TileBasePath + "Door_Frame_Left.png");
                AppendIfLoaded(layers, TileBasePath + "Door_Frame_Right.png");
                AppendIfLoaded(layers, TileBasePath + (isDoorOpen ? "Door_Open.png" : "Door_Closed.png"));
                break;
            case TileType.Wall:
                var wallOverlayPath = ResolveWallPath(world, position);
                AppendIfLoaded(layers, WallBasePath);
                if (!string.Equals(wallOverlayPath, WallBasePath, StringComparison.Ordinal))
                {
                    AppendIfLoaded(layers, wallOverlayPath);
                }
                break;
        }

        return layers;
    }

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
        if (entity.GetComponent<ChestComponent>() is not null)
        {
            return null;
        }

        return entity.Faction switch
        {
            Faction.Player => PlayerVisualCatalog.GetBaseTexture(entity),
            Faction.Neutral => PlayerVisualCatalog.GetBaseTexture(entity),
            Faction.Enemy => ResolveEnemyTexture(entity.Name),
            _ => null,
        };
    }

    public static TileBoundaryMask GetWalkableBoundaryMask(IWorldState? world, Position position, TileType tileType)
    {
        if (!ShouldDrawBoundaryTrim(world, position, tileType))
        {
            return default;
        }

        return new TileBoundaryMask(
            North: IsSolidBoundary(world, new Position(position.X, position.Y - 1)),
            East: IsSolidBoundary(world, new Position(position.X + 1, position.Y)),
            South: IsSolidBoundary(world, new Position(position.X, position.Y + 1)),
            West: IsSolidBoundary(world, new Position(position.X - 1, position.Y)));
    }

    public static bool HasNorthWallCover(IWorldState? world, Position position, TileType tileType)
    {
        return ShouldDrawBoundaryTrim(world, position, tileType)
            && IsSolidBoundary(world, new Position(position.X, position.Y - 1));
    }

    private static Texture2D? ResolveEnemyTexture(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return normalized switch
        {
            "giant rat" => Load(SpriteBasePath + "Imp_Idle_1.png"),
            "skeleton warrior" => Load(SpriteBasePath + "Skelet_Idle_1.png"),
            "goblin archer" => Load(SpriteBasePath + "Goblin_Idle_1.png"),
            "orc brute" => Load(SpriteBasePath + "Ogre_Idle_1.png"),
            "spectral wraith" => Load(SpriteBasePath + "Ice_Zombie_Idle_1.png"),
            "acid slime" => Load(SpriteBasePath + "Swampy_Idle_1.png"),
            "cave spider" => Load(SpriteBasePath + "Wogol_Idle_1.png"),
            "skeleton knight" => Load(SpriteBasePath + "Orc_Warrior_Idle_1.png"),
            "goblin shaman" => Load(SpriteBasePath + "Orc_Shaman_Idle_1.png"),
            "dark mage" => Load(SpriteBasePath + "Necromancer_Idle_1.png"),
            "shadow stalker" => Load(SpriteBasePath + "Masked_Orc_Idle_1.png"),
            "bone lord" => Load(SpriteBasePath + "Big_Zombie_Idle_1.png"),
            "flame elemental" => Load(SpriteBasePath + "Chort_Idle_1.png"),
            _ => ResolveEnemyTextureFallback(normalized),
        };
    }

    private static Texture2D? ResolveEnemyTextureFallback(string normalized)
    {
        if (normalized.Contains("rat", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Imp_Idle_1.png");
        }

        if (normalized.Contains("spider", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Wogol_Idle_1.png");
        }

        if (normalized.Contains("slime", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Swampy_Idle_1.png");
        }

        if (normalized.Contains("goblin", StringComparison.Ordinal))
        {
            if (normalized.Contains("shaman", StringComparison.Ordinal))
            {
                return Load(SpriteBasePath + "Orc_Shaman_Idle_1.png");
            }

            return Load(SpriteBasePath + "Goblin_Idle_1.png");
        }

        if (normalized.Contains("shaman", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Orc_Shaman_Idle_1.png");
        }

        if (normalized.Contains("orc", StringComparison.Ordinal))
        {
            return normalized.Contains("brute", StringComparison.Ordinal)
                ? Load(SpriteBasePath + "Ogre_Idle_1.png")
                : Load(SpriteBasePath + "Orc_Warrior_Idle_1.png");
        }

        if (normalized.Contains("mage", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Necromancer_Idle_1.png");
        }

        if (normalized.Contains("shadow", StringComparison.Ordinal))
        {
            return normalized.Contains("stalker", StringComparison.Ordinal)
                ? Load(SpriteBasePath + "Wogol_Idle_1.png")
                : Load(SpriteBasePath + "Masked_Orc_Idle_1.png");
        }

        if (normalized.Contains("wraith", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Ice_Zombie_Idle_1.png");
        }

        if (normalized.Contains("skeleton", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Skelet_Idle_1.png");
        }

        if (normalized.Contains("bone", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Big_Zombie_Idle_1.png");
        }

        if (normalized.Contains("zombie", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Zombie_Idle_1.png");
        }

        if (normalized.Contains("flame", StringComparison.Ordinal)
            || normalized.Contains("elemental", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Chort_Idle_1.png");
        }

        if (normalized.Contains("demon", StringComparison.Ordinal))
        {
            return Load(SpriteBasePath + "Big_Demon_Idle_1.png");
        }

        return null;
    }

    private static void AppendIfLoaded(List<Texture2D> layers, string path)
    {
        if (Load(path) is { } texture)
        {
            layers.Add(texture);
        }
    }

    private static string ResolveFloorPath(Position position, bool allowCracks)
    {
        if (!allowCracks)
        {
            return TileBasePath + "Floor_Clean.png";
        }

        var hash = PositiveModulo((position.X * 73856093) ^ (position.Y * 19349663), 12);
        return hash < FloorVariantPaths.Length
            ? FloorVariantPaths[hash]
            : TileBasePath + "Floor_Clean.png";
    }

    private static string ResolveWallPath(IWorldState? world, Position position)
    {
        var north = new Position(position.X, position.Y - 1);
        var south = new Position(position.X, position.Y + 1);
        var east = new Position(position.X + 1, position.Y);
        var west = new Position(position.X - 1, position.Y);
        var northEast = new Position(position.X + 1, position.Y - 1);
        var northWest = new Position(position.X - 1, position.Y - 1);
        var southEast = new Position(position.X + 1, position.Y + 1);
        var southWest = new Position(position.X - 1, position.Y + 1);

        var southOpen = IsOpenGround(world, south);
        var northOpen = IsOpenGround(world, north);
        var eastOpen = IsOpenGround(world, east);
        var westOpen = IsOpenGround(world, west);
        var northEastOpen = IsOpenGround(world, northEast);
        var northWestOpen = IsOpenGround(world, northWest);
        var southEastOpen = IsOpenGround(world, southEast);
        var southWestOpen = IsOpenGround(world, southWest);

        if (southOpen)
        {
            if (eastOpen && !westOpen)
            {
                return TileBasePath + "Wall_Top_Left.png";
            }

            if (westOpen && !eastOpen)
            {
                return TileBasePath + "Wall_Top_Right.png";
            }

            if (southEastOpen && !southWestOpen)
            {
                return TileBasePath + "Wall_Corner_Top_Left.png";
            }

            if (southWestOpen && !southEastOpen)
            {
                return TileBasePath + "Wall_Corner_Top_Right.png";
            }

            return TileBasePath + "Wall_Top_Mid.png";
        }

        if (eastOpen)
        {
            if (northEastOpen && !southEastOpen)
            {
                return TileBasePath + "Wall_Side_Front_Left.png";
            }

            if (southEastOpen && !northEastOpen)
            {
                return TileBasePath + "Wall_Side_Top_Left.png";
            }

            if (northEastOpen || southEastOpen)
            {
                return TileBasePath + "Wall_Side_Mid_Left.png";
            }

            return TileBasePath + "Wall_Left.png";
        }

        if (westOpen)
        {
            if (northWestOpen && !southWestOpen)
            {
                return TileBasePath + "Wall_Side_Front_Right.png";
            }

            if (southWestOpen && !northWestOpen)
            {
                return TileBasePath + "Wall_Side_Top_Right.png";
            }

            if (northWestOpen || southWestOpen)
            {
                return TileBasePath + "Wall_Side_Mid_Right.png";
            }

            return TileBasePath + "Wall_Right.png";
        }

        if (northOpen)
        {
            if (northEastOpen && !northWestOpen)
            {
                return TileBasePath + "Wall_Corner_Front_Left.png";
            }

            if (northWestOpen && !northEastOpen)
            {
                return TileBasePath + "Wall_Corner_Front_Right.png";
            }
        }

        if (southEastOpen)
        {
            return TileBasePath + "Wall_Corner_Front_Left.png";
        }

        if (southWestOpen)
        {
            return TileBasePath + "Wall_Corner_Front_Right.png";
        }

        return TileBasePath + "Wall_Mid.png";
    }

    private static bool ShouldDrawBoundaryTrim(IWorldState? world, Position position, TileType tileType)
    {
        if (tileType is not (TileType.Floor or TileType.StairsUp or TileType.StairsDown or TileType.Door))
        {
            return false;
        }

        if (tileType != TileType.Door)
        {
            return true;
        }

        return world is WorldState state && state.IsDoorOpen(position);
    }

    private static bool IsSolidBoundary(IWorldState? world, Position position)
    {
        if (world is null || !world.InBounds(position))
        {
            return true;
        }

        var tile = world.GetTile(position);
        if (tile == TileType.Wall)
        {
            return true;
        }

        return tile == TileType.Door && !(world is WorldState state && state.IsDoorOpen(position));
    }

    private static bool IsOpenGround(IWorldState? world, Position position)
    {
        if (world is null || !world.InBounds(position))
        {
            return false;
        }

        var tile = world.GetTile(position);
        if (tile == TileType.Wall)
        {
            return false;
        }

        if (tile != TileType.Door)
        {
            return true;
        }

        return world is WorldState state && state.IsDoorOpen(position);
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
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