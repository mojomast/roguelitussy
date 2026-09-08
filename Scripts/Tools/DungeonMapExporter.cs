using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed record DungeonMapExportResult(
    bool Success,
    string OutputPath,
    string AbsolutePath,
    int Width,
    int Height,
    string Message);

public static class DungeonMapExporter
{
    public const string DefaultExportDirectory = "user://map_exports";
    public const int TilePixels = 16;
    public const int MaximumDepth = 999;

    private const int Margin = 32;
    private const int HeaderHeight = 72;
    private const int FooterHeight = 88;

    private sealed record MapPalette(
        Color Background,
        Color Frame,
        Color Wall,
        Color WallEdge,
        Color Floor,
        Color FloorAccent,
        Color ThemeAccent,
        Color Text);

    private sealed record Marker(Position Position, char Glyph, Color Color, int Priority);

    public static DungeonMapExportResult Export(
        IGenerator generator,
        IContentDatabase content,
        int seed,
        int depth,
        string? outputDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(content);

        if (seed <= 0)
        {
            return Failure("Seed must be a positive whole number.");
        }

        if (depth < 0 || depth > MaximumDepth)
        {
            return Failure($"Depth must be between 0 and {MaximumDepth}.");
        }

        try
        {
            var world = new WorldState { ContentDatabase = content };
            var level = generator.GenerateLevel(world, seed, depth);
            var image = Render(world, level, seed, depth);
            var directory = string.IsNullOrWhiteSpace(outputDirectory) ? DefaultExportDirectory : outputDirectory;
            var fileName = $"dungeon_seed_{seed}_depth_{depth}.png";
            var outputPath = CombinePath(directory!, fileName);
            var absolutePath = ProjectSettings.GlobalizePath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ProjectSettings.GlobalizePath(directory!));

            var error = image.SavePng(outputPath);
            if (error != Error.Ok)
            {
                return Failure($"Godot could not write the PNG ({error}).", outputPath, absolutePath, image.GetWidth(), image.GetHeight());
            }

            return new DungeonMapExportResult(
                true,
                outputPath,
                absolutePath,
                image.GetWidth(),
                image.GetHeight(),
                $"Exported seed {seed}, depth {depth} dungeon map to {outputPath}.");
        }
        catch (Exception ex)
        {
            return Failure($"Dungeon map export failed: {ex.Message}");
        }
    }

    public static Image Render(WorldState world, LevelData level, int seed, int depth)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(level);

        var mapWidth = world.Width * TilePixels;
        var mapHeight = world.Height * TilePixels;
        var width = mapWidth + (Margin * 2);
        var height = mapHeight + HeaderHeight + FooterHeight + (Margin * 2);
        var mapX = Margin;
        var mapY = Margin + HeaderHeight;
        var palette = ResolvePalette(depth);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(palette.Background);

        Fill(image, Margin - 4, mapY - 4, mapWidth + 8, mapHeight + 8, palette.Frame);
        Fill(image, mapX, mapY, mapWidth, mapHeight, palette.Background);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                DrawTile(image, world, new Position(x, y), mapX + (x * TilePixels), mapY + (y * TilePixels), seed, depth, palette);
            }
        }

        DrawRoomCorners(image, level.Rooms, mapX, mapY, palette.ThemeAccent);
        DrawMarkers(image, BuildMarkers(level), mapX, mapY, palette.Background);
        DrawHeader(image, seed, depth, world.Width, world.Height, palette);
        DrawFooter(image, mapY + mapHeight + 20, palette);
        return image;
    }

    private static void DrawTile(
        Image image,
        WorldState world,
        Position position,
        int pixelX,
        int pixelY,
        int seed,
        int depth,
        MapPalette palette)
    {
        var tile = world.GetTile(position);
        switch (tile)
        {
            case TileType.Void:
                return;
            case TileType.Wall:
                Fill(image, pixelX, pixelY, TilePixels, TilePixels, palette.Wall);
                Fill(image, pixelX, pixelY, TilePixels, 2, palette.WallEdge);
                Fill(image, pixelX, pixelY, 2, TilePixels, palette.WallEdge);
                return;
            case TileType.Water:
                FillFloor(image, pixelX, pixelY, new Color("173653"), palette.FloorAccent);
                Fill(image, pixelX + 3, pixelY + 5, 10, 1, new Color("56899A"));
                Fill(image, pixelX + 1, pixelY + 11, 8, 1, new Color("365F73"));
                return;
            case TileType.Lava:
                FillFloor(image, pixelX, pixelY, new Color("381616"), palette.FloorAccent);
                Fill(image, pixelX + 2, pixelY + 7, 12, 2, new Color("E5532D"));
                Fill(image, pixelX + 7, pixelY + 3, 2, 10, new Color("FFD166"));
                return;
            default:
                FillFloor(image, pixelX, pixelY, palette.Floor, palette.FloorAccent);
                break;
        }

        var visualHash = StableHash(seed, depth, position.X, position.Y, (int)tile);
        if ((visualHash & 7) == 0 && tile == TileType.Floor)
        {
            image.SetPixel(pixelX + 4 + ((visualHash >> 3) & 7), pixelY + 4 + ((visualHash >> 6) & 7), palette.FloorAccent);
        }

        switch (tile)
        {
            case TileType.Door:
                DrawDoor(image, pixelX, pixelY, new Color("9B642E"), new Color("D2A85C"));
                break;
            case TileType.LockedDoor:
                DrawDoor(image, pixelX, pixelY, new Color("69401F"), new Color("FFD166"));
                DrawGlyph(image, 'L', pixelX + 5, pixelY + 4, 1, new Color("FFF0A8"));
                break;
            case TileType.StairsUp:
                DrawGlyph(image, '<', pixelX + 5, pixelY + 4, 1, new Color("9BCB9A"));
                break;
            case TileType.StairsDown:
                DrawGlyph(image, '>', pixelX + 5, pixelY + 4, 1, new Color("FFD166"));
                break;
            case TileType.Trap:
                DrawTrap(image, pixelX, pixelY, new Color("D67A62"));
                break;
        }
    }

    private static void FillFloor(Image image, int x, int y, Color floor, Color edge)
    {
        Fill(image, x, y, TilePixels, TilePixels, floor);
        Fill(image, x, y, TilePixels, 1, edge);
        Fill(image, x, y, 1, TilePixels, edge);
    }

    private static void DrawDoor(Image image, int x, int y, Color body, Color trim)
    {
        Fill(image, x + 3, y + 2, 10, 12, trim);
        Fill(image, x + 5, y + 3, 6, 11, body);
        image.SetPixel(x + 9, y + 8, trim);
    }

    private static void DrawTrap(Image image, int x, int y, Color color)
    {
        for (var row = 0; row < 6; row++)
        {
            image.SetPixel(x + 8 - row, y + 5 + row, color);
            image.SetPixel(x + 8 + row, y + 5 + row, color);
        }

        Fill(image, x + 3, y + 11, 11, 1, color);
    }

    private static void DrawRoomCorners(Image image, IReadOnlyList<RoomData> rooms, int mapX, int mapY, Color color)
    {
        foreach (var room in rooms.OrderBy(room => room.Y).ThenBy(room => room.X).ThenBy(room => room.PrefabId, StringComparer.Ordinal))
        {
            var x = mapX + (room.X * TilePixels);
            var y = mapY + (room.Y * TilePixels);
            var width = room.Width * TilePixels;
            var height = room.Height * TilePixels;
            Fill(image, x + 2, y + 2, 10, 1, color);
            Fill(image, x + 2, y + 2, 1, 10, color);
            Fill(image, x + width - 12, y + 2, 10, 1, color);
            Fill(image, x + width - 3, y + 2, 1, 10, color);
            Fill(image, x + 2, y + height - 3, 10, 1, color);
            Fill(image, x + 2, y + height - 12, 1, 10, color);
            Fill(image, x + width - 12, y + height - 3, 10, 1, color);
            Fill(image, x + width - 3, y + height - 12, 1, 10, color);
        }
    }

    private static IReadOnlyList<Marker> BuildMarkers(LevelData level)
    {
        var markers = new List<Marker>
        {
            new(level.PlayerSpawn, 'P', new Color("A9E7FF"), 100),
        };

        var enemies = level.EnemySpawnDetails
            ?? level.EnemySpawns.Select(position => new EnemySpawnData(position)).ToArray();
        var items = level.ItemSpawnDetails
            ?? level.ItemSpawns.Select(position => new ItemSpawnData(position)).ToArray();
        var chests = level.ChestSpawnDetails
            ?? (level.ChestSpawns ?? Array.Empty<Position>()).Select(position => new ChestSpawnData(position)).ToArray();

        markers.AddRange(enemies
            .Select(spawn => new Marker(spawn.Position, spawn.IsBoss ? 'B' : 'E', spawn.IsBoss ? new Color("FF8B5E") : new Color("E45B5B"), spawn.IsBoss ? 90 : 80)));
        markers.AddRange(items
            .Select(spawn => new Marker(spawn.Position, 'I', new Color("FFE08A"), 60)));
        markers.AddRange(chests
            .Select(spawn => new Marker(spawn.Position, 'C', new Color("D99A45"), 70)));
        markers.AddRange((level.NpcSpawns ?? Array.Empty<NpcSpawnData>())
            .Select(spawn => new Marker(spawn.Position, 'N', new Color("83C995"), 75)));
        markers.AddRange((level.KeySpawns ?? Array.Empty<Position>())
            .Select(position => new Marker(position, 'K', new Color("FFF0A8"), 85)));

        return markers
            .OrderBy(marker => marker.Position.Y)
            .ThenBy(marker => marker.Position.X)
            .ThenBy(marker => marker.Priority)
            .ToArray();
    }

    private static void DrawMarkers(Image image, IReadOnlyList<Marker> markers, int mapX, int mapY, Color background)
    {
        foreach (var marker in markers)
        {
            var x = mapX + (marker.Position.X * TilePixels) + 4;
            var y = mapY + (marker.Position.Y * TilePixels) + 3;
            Fill(image, x - 2, y - 2, 9, 11, background);
            DrawGlyph(image, marker.Glyph, x, y, 1, marker.Color);
        }
    }

    private static void DrawHeader(Image image, int seed, int depth, int mapWidth, int mapHeight, MapPalette palette)
    {
        var theme = ResolveTheme(depth).ToUpperInvariant();
        var title = ResolveTitle(seed, depth, theme);
        DrawText(image, title, Margin, Margin, 2, palette.Text, 2);
        DrawText(
            image,
            $"{theme}  DEPTH {depth}  SEED {seed}  {mapWidth}X{mapHeight}",
            Margin,
            Margin + 34,
            1,
            palette.ThemeAccent,
            1);
    }

    private static void DrawFooter(Image image, int y, MapPalette palette)
    {
        DrawText(image, "P START  E ENEMY  B BOSS  I ITEM  C CHEST  N NPC  K KEY", Margin, y, 1, palette.Text, 1);
        DrawText(image, "< STAIRS UP  > STAIRS DOWN  L LOCKED  ^ TRAP", Margin, y + 20, 1, palette.ThemeAccent, 1);
        DrawText(image, "DETERMINISTIC DUNGEON SURVEY", Margin, y + 46, 1, palette.FloorAccent, 1);
    }

    private static string ResolveTitle(int seed, int depth, string theme)
    {
        var names = theme switch
        {
            "PRISON" => new[] { "THE IRON WARD", "THE SUNKEN GAOL", "THE RUSTED CELLS" },
            "CRYPT" => new[] { "THE HOLLOW SEPULCHRE", "THE ASHEN VAULT", "THE BONE ARCHIVE" },
            _ => new[] { "THE EMBER DEEP", "THE CINDER FORGE", "THE BASALT THRONE" },
        };
        var index = (int)((uint)StableHash(seed, depth, 0, 0, 0x51f15e) % (uint)names.Length);
        return names[index];
    }

    private static MapPalette ResolvePalette(int depth)
    {
        return ResolveTheme(depth) switch
        {
            "prison" => new MapPalette(new Color("090B0D"), new Color("A65A32"), new Color("171D22"), new Color("56646B"), new Color("39444A"), new Color("89959B"), new Color("56899A"), new Color("F2E6CD")),
            "crypt" => new MapPalette(new Color("0B090D"), new Color("D2C6A7"), new Color("19171B"), new Color("625E57"), new Color("403E39"), new Color("918A7C"), new Color("719587"), new Color("F1E7D2")),
            _ => new MapPalette(new Color("100908"), new Color("E5532D"), new Color("211414"), new Color("6E4034"), new Color("4B332C"), new Color("915A47"), new Color("FFD166"), new Color("F8E4CF")),
        };
    }

    private static string ResolveTheme(int depth)
    {
        var floor = Math.Max(1, depth);
        return floor <= 3 ? "prison" : floor <= 6 ? "crypt" : "magma";
    }

    private static int StableHash(int seed, int depth, int x, int y, int salt)
    {
        unchecked
        {
            var hash = seed ^ (depth * 7919) ^ salt;
            hash = (hash * 16777619) ^ x;
            hash = (hash * 16777619) ^ y;
            hash ^= hash << 13;
            hash ^= (int)((uint)hash >> 17);
            return hash ^ (hash << 5);
        }
    }

    private static void DrawText(Image image, string text, int x, int y, int scale, Color color, int spacing)
    {
        var cursorX = x;
        foreach (var character in text.ToUpperInvariant())
        {
            DrawGlyph(image, character, cursorX, y, scale, color);
            cursorX += (5 * scale) + spacing;
        }
    }

    private static void DrawGlyph(Image image, char character, int x, int y, int scale, Color color)
    {
        var rows = GetGlyph(character);
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] == '1')
                {
                    Fill(image, x + (column * scale), y + (row * scale), scale, scale, color);
                }
            }
        }
    }

    private static string[] GetGlyph(char character) => character switch
    {
        'A' => ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        'B' => ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        'C' => ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        'D' => ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        'E' => ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        'F' => ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        'G' => ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
        'H' => ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        'I' => ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
        'J' => ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
        'K' => ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        'L' => ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        'M' => ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        'N' => ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        'O' => ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        'P' => ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        'Q' => ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        'R' => ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        'S' => ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        'T' => ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        'U' => ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        'V' => ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        'W' => ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
        'X' => ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        'Y' => ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        'Z' => ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        '0' => ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        '1' => ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        '2' => ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        '3' => ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        '4' => ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        '5' => ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        '6' => ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        '7' => ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        '8' => ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        '9' => ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        '<' => ["00010", "00100", "01000", "10000", "01000", "00100", "00010"],
        '>' => ["01000", "00100", "00010", "00001", "00010", "00100", "01000"],
        '^' => ["00100", "01010", "10001", "00000", "00000", "00000", "00000"],
        '-' => ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
        ':' => ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
        _ => ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
    };

    private static void Fill(Image image, int x, int y, int width, int height, Color color)
    {
        if (width > 0 && height > 0)
        {
            image.FillRect(new Rect2I(x, y, width, height), color);
        }
    }

    private static string CombinePath(string directory, string fileName)
    {
        if (directory.Contains("://", StringComparison.Ordinal))
        {
            return directory.TrimEnd('/') + "/" + fileName;
        }

        return Path.Combine(directory, fileName);
    }

    private static DungeonMapExportResult Failure(
        string message,
        string outputPath = "",
        string absolutePath = "",
        int width = 0,
        int height = 0) => new(false, outputPath, absolutePath, width, height, message);
}
