using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class MapEditor : Control
{
    public enum ToolMode
    {
        Pen,
        Rect,
        Fill,
    }

    private const int DefaultWidth = 20;
    private const int DefaultHeight = 20;
    private const int CellSize = 24;
    private const int CanvasOffsetX = 200;
    private const int CanvasOffsetY = 40;
    private static readonly Regex StableIdPattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Dictionary<char, string> DefaultLegend = new()
    {
        ['#'] = "wall",
        ['.'] = "floor",
        ['+'] = "door",
        ['>'] = "stairs_down",
        ['<'] = "stairs_up",
        ['~'] = "water",
        ['^'] = "trap",
        ['S'] = "spawn_enemy",
        ['I'] = "spawn_item",
        ['P'] = "spawn_player",
        ['C'] = "chest",
        [' '] = "void",
    };

    private static readonly Dictionary<char, Color> BrushColors = new()
    {
        ['#'] = new Color(0.15f, 0.15f, 0.15f),
        ['.'] = new Color(0.45f, 0.45f, 0.45f),
        ['+'] = new Color(0.63f, 0.35f, 0.1f),
        ['>'] = new Color(0.95f, 0.8f, 0.25f),
        ['<'] = new Color(0.2f, 0.8f, 0.35f),
        ['~'] = new Color(0.18f, 0.35f, 0.82f),
        ['^'] = new Color(0.82f, 0.2f, 0.2f),
        ['S'] = new Color(0.8f, 0.2f, 0.75f),
        ['I'] = new Color(0.85f, 0.7f, 0.15f),
        ['P'] = new Color(0.2f, 0.9f, 0.95f),
        ['C'] = new Color(0.72f, 0.55f, 0.15f),
        [' '] = new Color(0.04f, 0.04f, 0.04f),
    };

    private char[,] _canvas = new char[DefaultWidth, DefaultHeight];
    private int _canvasWidth = DefaultWidth;
    private int _canvasHeight = DefaultHeight;
    private bool _isPrimaryButtonDown;
    private Vector2I? _rectAnchor;

    public MapEditor()
    {
        Name = "MapEditor";
        RoomId = "new_room";
        RoomName = "New Room";
        TagsText = "custom";
        MinDepth = 1;
        MaxDepth = 99;
        SelectedBrush = '#';
        CurrentTool = ToolMode.Pen;
        StatusText = "Ready.";
        CustomMinimumSize = new Vector2(860f, 600f);
        ResetCanvas();
    }

    public string RoomId { get; private set; }

    public string RoomName { get; private set; }

    public string TagsText { get; private set; }

    public int MinDepth { get; private set; }

    public int MaxDepth { get; private set; }

    public char SelectedBrush { get; private set; }

    public ToolMode CurrentTool { get; private set; }

    public string StatusText { get; private set; }

    public int CanvasWidth => _canvasWidth;

    public int CanvasHeight => _canvasHeight;

    public IReadOnlyDictionary<char, string> BrushLegend => DefaultLegend;

    public string CanvasText => string.Join("\n", GetLayoutRows());

    public IReadOnlyList<string> GetRoomIds(string? contentDirectory = null)
    {
        var path = ToolPaths.ResolveContentFile("room_prefabs.json", contentDirectory);
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        return ToolJson
            .Read<RoomPrefabsDocument>(path)
            .Rooms
            .OrderBy(room => room.Id, StringComparer.Ordinal)
            .Select(room => room.Id)
            .ToArray();
    }

    public override void _Ready()
    {
        try
        {
            var firstRoom = ToolJson.Read<RoomPrefabsDocument>(ToolPaths.ResolveContentFile("room_prefabs.json")).Rooms.FirstOrDefault();
            if (firstRoom is not null)
            {
                LoadFromPrefab(firstRoom);
                StatusText = $"Loaded '{firstRoom.Id}'.";
            }
        }
        catch
        {
            ResetCanvas();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && button.Pressed)
        {
            _isPrimaryButtonDown = true;
            ApplyToolAt(ScreenToGrid(button.Position));
            return;
        }

        if (@event is InputEventMouseButton released && released.ButtonIndex == MouseButton.Left && !released.Pressed)
        {
            _isPrimaryButtonDown = false;
            return;
        }

        if (@event is InputEventMouseMotion motion && _isPrimaryButtonDown && CurrentTool == ToolMode.Pen)
        {
            PaintCell(ScreenToGrid(motion.Position).X, ScreenToGrid(motion.Position).Y);
        }
    }

    public override void _Draw()
    {
        for (var y = 0; y < _canvasHeight; y++)
        {
            for (var x = 0; x < _canvasWidth; x++)
            {
                var rect = new Rect2(
                    new Vector2(CanvasOffsetX + (x * CellSize), CanvasOffsetY + (y * CellSize)),
                    new Vector2(CellSize - 1f, CellSize - 1f));
                DrawRect(rect, ResolveBrushColor(_canvas[x, y]));
            }
        }
    }

    public void SetMetadata(string roomId, string roomName, string tags, int minDepth, int maxDepth)
    {
        RoomId = string.IsNullOrWhiteSpace(roomId) ? RoomId : roomId.Trim();
        RoomName = string.IsNullOrWhiteSpace(roomName) ? ToDisplayName(RoomId) : roomName.Trim();
        TagsText = string.IsNullOrWhiteSpace(tags) ? string.Empty : tags.Trim();
        MinDepth = Math.Max(1, minDepth);
        MaxDepth = Math.Max(MinDepth, maxDepth);
    }

    public void SetSelectedBrush(char brush)
    {
        if (!DefaultLegend.ContainsKey(brush))
        {
            throw new ArgumentOutOfRangeException(nameof(brush), $"Unknown brush '{brush}'.");
        }

        SelectedBrush = brush;
        StatusText = $"Brush set to '{brush}' ({DefaultLegend[brush]}).";
    }

    public void SetTool(ToolMode mode)
    {
        CurrentTool = mode;
        _rectAnchor = null;
        StatusText = $"Tool set to {mode}.";
    }

    public void CreateDraft(string id, int width = DefaultWidth, int height = DefaultHeight)
    {
        ResizeCanvas(Math.Max(4, width), Math.Max(4, height), '#');
        for (var y = 1; y < _canvasHeight - 1; y++)
        {
            for (var x = 1; x < _canvasWidth - 1; x++)
            {
                _canvas[x, y] = '.';
            }
        }

        var midY = Math.Clamp(_canvasHeight / 2, 1, _canvasHeight - 2);
        _canvas[0, midY] = '+';
        _canvas[_canvasWidth - 1, midY] = '+';
        _canvas[1, 1] = 'P';
        _canvas[_canvasWidth - 2, _canvasHeight - 2] = '>';
        SetMetadata(id, ToDisplayName(id), "custom", 1, 99);
        _rectAnchor = null;
        QueueRedraw();
        StatusText = $"Created draft '{id}'.";
    }

    public void ResizeCanvas(int width, int height, char fill = '#')
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas dimensions must be positive.");
        }

        var normalizedFill = NormalizeBrush(fill);
        var resized = new char[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                resized[x, y] = x < _canvasWidth && y < _canvasHeight ? _canvas[x, y] : normalizedFill;
            }
        }

        _canvas = resized;
        _canvasWidth = width;
        _canvasHeight = height;
        QueueRedraw();
        StatusText = $"Canvas resized to {width}x{height}.";
    }

    public char GetCell(int x, int y)
    {
        return IsInBounds(x, y) ? _canvas[x, y] : ' ';
    }

    public bool PaintCell(int x, int y, char? brush = null)
    {
        if (!IsInBounds(x, y))
        {
            return false;
        }

        _canvas[x, y] = NormalizeBrush(brush ?? SelectedBrush);
        QueueRedraw();
        return true;
    }

    public bool ApplyCurrentToolAt(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            StatusText = $"Cell {x},{y} is out of bounds.";
            return false;
        }

        ApplyToolAt(new Vector2I(x, y));
        return true;
    }

    public bool PaintRectangle(int startX, int startY, int endX, int endY, char? brush = null)
    {
        var normalizedBrush = NormalizeBrush(brush ?? SelectedBrush);
        if (!IsInBounds(startX, startY) && !IsInBounds(endX, endY))
        {
            return false;
        }

        var minX = Math.Max(0, Math.Min(startX, endX));
        var minY = Math.Max(0, Math.Min(startY, endY));
        var maxX = Math.Min(_canvasWidth - 1, Math.Max(startX, endX));
        var maxY = Math.Min(_canvasHeight - 1, Math.Max(startY, endY));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                _canvas[x, y] = normalizedBrush;
            }
        }

        QueueRedraw();
        return true;
    }

    public bool FloodFill(int startX, int startY, char? brush = null)
    {
        if (!IsInBounds(startX, startY))
        {
            return false;
        }

        var replacement = NormalizeBrush(brush ?? SelectedBrush);
        var target = _canvas[startX, startY];
        if (target == replacement)
        {
            return false;
        }

        var pending = new Queue<Vector2I>();
        pending.Enqueue(new Vector2I(startX, startY));

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!IsInBounds(current.X, current.Y) || _canvas[current.X, current.Y] != target)
            {
                continue;
            }

            _canvas[current.X, current.Y] = replacement;
            pending.Enqueue(new Vector2I(current.X + 1, current.Y));
            pending.Enqueue(new Vector2I(current.X - 1, current.Y));
            pending.Enqueue(new Vector2I(current.X, current.Y + 1));
            pending.Enqueue(new Vector2I(current.X, current.Y - 1));
        }

        QueueRedraw();
        return true;
    }

    public void ResetCanvas(char fill = '#')
    {
        _canvasWidth = DefaultWidth;
        _canvasHeight = DefaultHeight;
        _canvas = new char[_canvasWidth, _canvasHeight];
        var normalizedFill = NormalizeBrush(fill);
        for (var y = 0; y < _canvasHeight; y++)
        {
            for (var x = 0; x < _canvasWidth; x++)
            {
                _canvas[x, y] = normalizedFill;
            }
        }

        QueueRedraw();
        StatusText = "Canvas reset.";
    }

    public bool LoadPrefab(string id, string? contentDirectory = null)
    {
        var path = ToolPaths.ResolveContentFile("room_prefabs.json", contentDirectory);
        var document = ToolJson.Read<RoomPrefabsDocument>(path);
        var room = document.Rooms.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (room is null)
        {
            StatusText = $"Room '{id}' was not found.";
            return false;
        }

        LoadFromPrefab(room);
        StatusText = $"Loaded '{room.Id}'.";
        return true;
    }

    public void LoadFromPrefab(RoomPrefabDefinition room)
    {
        if (room.Layout.Count == 0)
        {
            throw new ArgumentException("Room layout cannot be empty.", nameof(room));
        }

        _canvasWidth = room.Width;
        _canvasHeight = room.Height;
        _canvas = new char[_canvasWidth, _canvasHeight];
        for (var y = 0; y < _canvasHeight; y++)
        {
            var row = y < room.Layout.Count ? room.Layout[y] : new string('#', _canvasWidth);
            for (var x = 0; x < _canvasWidth; x++)
            {
                var token = x < row.Length ? row[x] : '#';
                _canvas[x, y] = NormalizeBrush(token);
            }
        }

        RoomId = room.Id;
        RoomName = string.IsNullOrWhiteSpace(room.Name) ? ToDisplayName(room.Id) : room.Name;
        TagsText = string.Join(", ", room.Tags);
        MinDepth = room.MinDepth;
        MaxDepth = room.MaxDepth;
        _rectAnchor = null;
        QueueRedraw();
    }

    public RoomPrefabDefinition BuildPrefabDefinition()
    {
        ValidateMetadata();
        var layout = GetLayoutRows();
        return new RoomPrefabDefinition
        {
            Id = RoomId,
            Name = string.IsNullOrWhiteSpace(RoomName) ? ToDisplayName(RoomId) : RoomName,
            Tags = TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Width = _canvasWidth,
            Height = _canvasHeight,
            MinDepth = MinDepth,
            MaxDepth = MaxDepth,
            Layout = layout.ToList(),
            Doors = BuildDoors(layout),
            SpawnPoints = BuildSpawnPoints(layout),
            FixedEntities = new List<FixedEntityDefinition>(),
        };
    }

    public void SavePrefab(string? contentDirectory = null)
    {
        var path = ToolPaths.ResolveContentFile("room_prefabs.json", contentDirectory);
        var document = CreateOrLoadDocument(path);
        document.TileLegend = MergeLegend(document.TileLegend);

        var prefab = BuildPrefabDefinition();
        var existingIndex = document.Rooms.FindIndex(room => string.Equals(room.Id, prefab.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            document.Rooms[existingIndex] = prefab;
        }
        else
        {
            document.Rooms.Add(prefab);
        }

        document.Rooms = document.Rooms.OrderBy(room => room.Id, StringComparer.Ordinal).ToList();
        ToolJson.Write(path, document);
        StatusText = $"Saved '{prefab.Id}' to room_prefabs.json.";
    }

    public IReadOnlyList<string> ValidateDraft()
    {
        var errors = new List<string>();
        try
        {
            _ = BuildPrefabDefinition();
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        if (errors.Count == 0)
        {
            StatusText = $"Draft '{RoomId}' is valid.";
        }
        else
        {
            StatusText = errors[0];
        }

        return errors;
    }

    public IReadOnlyList<string> GetLayoutRows()
    {
        var rows = new List<string>(_canvasHeight);
        for (var y = 0; y < _canvasHeight; y++)
        {
            var builder = new StringBuilder(_canvasWidth);
            for (var x = 0; x < _canvasWidth; x++)
            {
                builder.Append(_canvas[x, y]);
            }

            rows.Add(builder.ToString());
        }

        return rows;
    }

    private void ApplyToolAt(Vector2I cell)
    {
        if (!IsInBounds(cell.X, cell.Y))
        {
            return;
        }

        switch (CurrentTool)
        {
            case ToolMode.Pen:
                PaintCell(cell.X, cell.Y);
                break;
            case ToolMode.Fill:
                FloodFill(cell.X, cell.Y);
                break;
            case ToolMode.Rect:
                if (_rectAnchor is null)
                {
                    _rectAnchor = cell;
                    StatusText = $"Rectangle anchor set at {cell.X},{cell.Y}.";
                }
                else
                {
                    PaintRectangle(_rectAnchor.Value.X, _rectAnchor.Value.Y, cell.X, cell.Y);
                    _rectAnchor = null;
                    StatusText = $"Rectangle painted to {cell.X},{cell.Y}.";
                }

                break;
        }
    }

    private Vector2I ScreenToGrid(Vector2 position)
    {
        var x = (int)((position.X - CanvasOffsetX) / CellSize);
        var y = (int)((position.Y - CanvasOffsetY) / CellSize);
        return new Vector2I(x, y);
    }

    private bool IsInBounds(int x, int y) => x >= 0 && x < _canvasWidth && y >= 0 && y < _canvasHeight;

    private static char NormalizeBrush(char brush)
    {
        if (!DefaultLegend.ContainsKey(brush))
        {
            return '#';
        }

        return brush;
    }

    private void ValidateMetadata()
    {
        if (!StableIdPattern.IsMatch(RoomId))
        {
            throw new InvalidOperationException($"Room id '{RoomId}' must use lowercase letters, digits, and underscores only.");
        }

        if (MinDepth <= 0 || MaxDepth < MinDepth)
        {
            throw new InvalidOperationException("Room depth range is invalid.");
        }
    }

    private static RoomPrefabsDocument CreateOrLoadDocument(string path)
    {
        if (System.IO.File.Exists(path))
        {
            return ToolJson.Read<RoomPrefabsDocument>(path);
        }

        return new RoomPrefabsDocument
        {
            Schema = "roguelike-room-prefabs-v1",
            Version = 1,
            TileLegend = new Dictionary<string, string>(StringComparer.Ordinal),
            Rooms = new List<RoomPrefabDefinition>(),
        };
    }

    private static Dictionary<string, string> MergeLegend(Dictionary<string, string> existing)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in existing)
        {
            merged[pair.Key] = pair.Value;
        }

        foreach (var pair in DefaultLegend)
        {
            merged[pair.Key.ToString()] = pair.Value;
        }

        return merged;
    }

    private static List<RoomDoorDefinition> BuildDoors(IReadOnlyList<string> layout)
    {
        var doors = new List<RoomDoorDefinition>();
        var height = layout.Count;
        var width = height == 0 ? 0 : layout[0].Length;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (layout[y][x] != '+')
                {
                    continue;
                }

                doors.Add(new RoomDoorDefinition
                {
                    X = x,
                    Y = y,
                    Direction = InferDoorDirection(x, y, width, height),
                });
            }
        }

        return doors;
    }

    private static string InferDoorDirection(int x, int y, int width, int height)
    {
        if (y == 0)
        {
            return "north";
        }

        if (y == height - 1)
        {
            return "south";
        }

        if (x == 0)
        {
            return "west";
        }

        if (x == width - 1)
        {
            return "east";
        }

        return "north";
    }

    private static List<RoomSpawnPointDefinition> BuildSpawnPoints(IReadOnlyList<string> layout)
    {
        var points = new List<RoomSpawnPointDefinition>();
        for (var y = 0; y < layout.Count; y++)
        {
            for (var x = 0; x < layout[y].Length; x++)
            {
                var type = layout[y][x] switch
                {
                    'P' => "player",
                    'S' => "enemy",
                    'I' => "item",
                    'C' => "chest",
                    '>' => "stairs_down",
                    '<' => "stairs_up",
                    '^' => "trap",
                    _ => string.Empty,
                };

                if (type.Length == 0)
                {
                    continue;
                }

                points.Add(new RoomSpawnPointDefinition
                {
                    X = x,
                    Y = y,
                    Type = type,
                });
            }
        }

        return points;
    }

    private static string ToDisplayName(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return "Untitled Room";
        }

        var words = stableId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static Color ResolveBrushColor(char brush)
    {
        return BrushColors.TryGetValue(brush, out var color) ? color : Colors.Black;
    }
}