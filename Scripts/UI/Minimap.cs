using System;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class Minimap : Control
{
    private const float OuterPadding = 16f;
    private const float InnerPadding = 10f;
    private const float DefaultSize = 220f;
    private const float LegendHeight = 42f;

    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private bool _suppressedByOverlay;
    private bool _legendVisible;
    private readonly Label _legendLabel;
    private static readonly MinimapLegendEntry[] Legend =
    {
        new("floor", "Floor", UiStyle.MapLineBlue(0.84f)),
        new("door", "Door", UiStyle.MapGold(0.95f)),
        new("stairs", "Stairs", UiStyle.BrightGold()),
        new("trap", "Trap", UiStyle.MapTrapRed()),
        new("player", "Player", UiStyle.BrightGold()),
        new("enemy", "Enemy", UiStyle.BloodRed()),
        new("npc", "NPC", UiStyle.ActiveGreen()),
        new("item", "Item", UiStyle.RarityUncommon()),
        new("chest", "Chest", UiStyle.WarningOrange()),
    };

    public Minimap()
    {
        Name = "Minimap";
        Size = new Vector2(DefaultSize, DefaultSize);
        CustomMinimumSize = Size;
        ZIndex = 35;
        _legendLabel = new Label
        {
            Name = "MinimapLegend",
            Text = BuildLegendText(),
            Modulate = UiStyle.Parchment(),
        };
        AddChild(_legendLabel);
        Refresh();
    }

    public bool MinimapEnabled { get; private set; } = true;

    public int VisibleTileCount { get; private set; }

    public int ExploredTileCount { get; private set; }

    public Roguelike.Core.Position PlayerWorldPosition { get; private set; } = Roguelike.Core.Position.Invalid;

    public string SummaryText { get; private set; } = "Minimap unavailable";

    public string LegendText => _legendLabel.Text;

    public bool LegendVisible => _legendVisible;

    public System.Collections.Generic.IReadOnlyList<MinimapLegendEntry> LegendEntries => Legend;

    public int EnemyMarkerCount { get; private set; }

    public int NpcMarkerCount { get; private set; }

    public int ItemMarkerCount { get; private set; }

    public int ChestMarkerCount { get; private set; }

    public int TrapTileCount { get; private set; }

    public int DoorTileCount { get; private set; }

    public int StairTileCount { get; private set; }

    public bool IsSuppressed => _suppressedByOverlay;

    public override void _Ready()
    {
        Refresh();
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnWorldChanged;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.FovRecalculated -= OnWorldChanged;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += OnWorldChanged;
            _eventBus.FloorChanged += OnFloorChanged;
            _eventBus.FovRecalculated += OnWorldChanged;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        Refresh();
    }

    public void Toggle()
    {
        MinimapEnabled = !MinimapEnabled;
        Refresh();
    }

    public void ToggleLegend()
    {
        _legendVisible = !_legendVisible;
        Refresh();
    }

    public void SetSuppressed(bool suppressed)
    {
        if (_suppressedByOverlay == suppressed)
        {
            return;
        }

        _suppressedByOverlay = suppressed;
        Refresh();
    }

    public void Refresh()
    {
        UpdatePlacement();

        var world = _gameManager?.World;
        var active = MinimapEnabled
            && !_suppressedByOverlay
            && world is not null
            && _gameManager?.CurrentState != GameManager.GameState.MainMenu;
        Visible = active;
        _legendLabel.Visible = active && _legendVisible;

        VisibleTileCount = 0;
        ExploredTileCount = 0;
        PlayerWorldPosition = Roguelike.Core.Position.Invalid;
        EnemyMarkerCount = 0;
        NpcMarkerCount = 0;
        ItemMarkerCount = 0;
        ChestMarkerCount = 0;
        TrapTileCount = 0;
        DoorTileCount = 0;
        StairTileCount = 0;

        if (world is null)
        {
            SummaryText = MinimapEnabled ? "Minimap unavailable" : "Minimap hidden";
            QueueRedraw();
            return;
        }

        var player = world.Player is null ? null : world.GetEntity(world.Player.Id);
        PlayerWorldPosition = player?.Position ?? Roguelike.Core.Position.Invalid;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Roguelike.Core.Position(x, y);
                if (world.IsVisible(position))
                {
                    VisibleTileCount++;
                }

                if (world.IsExplored(position))
                {
                    ExploredTileCount++;

                    switch (world.GetTile(position))
                    {
                        case TileType.Trap:
                            TrapTileCount++;
                            break;
                        case TileType.Door or TileType.LockedDoor:
                            DoorTileCount++;
                            break;
                        case TileType.StairsDown or TileType.StairsUp:
                            StairTileCount++;
                            break;
                    }
                }

                if (world.IsVisible(position))
                {
                    CountVisibleMarker(world, position);
                }
            }
        }

        SummaryText = MinimapEnabled
            ? $"Minimap: {ExploredTileCount} explored, {VisibleTileCount} visible"
            : "Minimap hidden";
        QueueRedraw();
    }

    public override void _Draw()
    {
        base._Draw();

        if (!Visible)
        {
            return;
        }

        var world = _gameManager?.World;
        if (world is null || world.Width <= 0 || world.Height <= 0)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), UiStyle.CathedralBlack(0.88f));
        DrawRect(new Rect2(Vector2.Zero, Size), UiStyle.GoldTrim(0.9f), filled: false);

        var availableWidth = Math.Max(1f, Size.X - (InnerPadding * 2f));
        var reservedLegendHeight = _legendVisible ? LegendHeight : 0f;
        var availableHeight = Math.Max(1f, Size.Y - (InnerPadding * 2f) - reservedLegendHeight);
        var cellSize = MathF.Min(availableWidth / world.Width, availableHeight / world.Height);
        if (cellSize <= 0f)
        {
            return;
        }

        var mapSize = new Vector2(cellSize * world.Width, cellSize * world.Height);
        var origin = new Vector2(
            InnerPadding + ((availableWidth - mapSize.X) * 0.5f),
            InnerPadding + ((availableHeight - mapSize.Y) * 0.5f));

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                var rect = new Rect2(
                    origin + new Vector2(x * cellSize, y * cellSize),
                    new Vector2(MathF.Max(1f, cellSize - 1f), MathF.Max(1f, cellSize - 1f)));
                DrawRect(rect, ResolveTileColor(world, position));
            }
        }

        DrawVisibleMarkers(world, origin, cellSize);

        if (PlayerWorldPosition != Roguelike.Core.Position.Invalid)
        {
            var inset = MathF.Max(1f, cellSize * 0.2f);
            var playerRect = new Rect2(
                origin + new Vector2(PlayerWorldPosition.X * cellSize, PlayerWorldPosition.Y * cellSize) + new Vector2(inset, inset),
                new Vector2(MathF.Max(1f, cellSize - (inset * 2f)), MathF.Max(1f, cellSize - (inset * 2f))));
            DrawRect(playerRect, UiStyle.BrightGold());
        }

        if (_legendVisible)
        {
            DrawLegendSwatches();
        }
    }

    public Color GetTileColor(Roguelike.Core.Position position)
    {
        var world = _gameManager?.World;
        return world is null ? Colors.Transparent : ResolveTileColor(world, position);
    }

    public Color GetLegendColor(string marker)
    {
        foreach (var entry in Legend)
        {
            if (string.Equals(entry.Marker, marker, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Color;
            }
        }

        return Colors.Transparent;
    }

    private void OnWorldChanged()
    {
        Refresh();
    }

    private void OnFloorChanged(int floor)
    {
        Refresh();
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Refresh();
        }
    }

    private void UpdatePlacement()
    {
        Size = new Vector2(DefaultSize, DefaultSize);
        CustomMinimumSize = Size;
        _legendLabel.Position = new Vector2(InnerPadding, Size.Y - LegendHeight + 17f);
        _legendLabel.Size = new Vector2(Size.X - (InnerPadding * 2f), LegendHeight - 18f);

        var viewportSize = GetParent() is not null && GetTree() is not null
            ? GetViewportRect().Size
            : new Vector2(1280f, 720f);
        Position = new Vector2(viewportSize.X - Size.X - OuterPadding, OuterPadding);
    }

    private void CountVisibleMarker(IWorldState world, Roguelike.Core.Position position)
    {
        var entity = world.GetEntityAt(position);
        if (entity is not null && entity.Id != world.Player?.Id)
        {
            if (entity.GetComponent<ChestComponent>() is not null)
            {
                ChestMarkerCount++;
                return;
            }

            if (entity.GetComponent<NpcComponent>() is not null)
            {
                NpcMarkerCount++;
                return;
            }

            if (entity.Faction == Faction.Enemy)
            {
                EnemyMarkerCount++;
                return;
            }
        }

        if (world is WorldState state && state.GetItemsAt(position).Count > 0)
        {
            ItemMarkerCount++;
        }
    }

    private void DrawVisibleMarkers(IWorldState world, Vector2 origin, float cellSize)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Roguelike.Core.Position(x, y);
                if (!world.IsVisible(position) || position == PlayerWorldPosition)
                {
                    continue;
                }

                var color = ResolveMarkerColor(world, position);
                if (color.A <= 0f)
                {
                    continue;
                }

                var inset = MathF.Max(1f, cellSize * 0.28f);
                DrawRect(
                    new Rect2(
                        origin + new Vector2(x * cellSize, y * cellSize) + new Vector2(inset, inset),
                        new Vector2(MathF.Max(1f, cellSize - (inset * 2f)), MathF.Max(1f, cellSize - (inset * 2f)))),
                    color);
            }
        }
    }

    private static Color ResolveMarkerColor(IWorldState world, Roguelike.Core.Position position)
    {
        var entity = world.GetEntityAt(position);
        if (entity is not null && entity.Id != world.Player?.Id)
        {
            if (entity.GetComponent<ChestComponent>() is not null)
            {
                return UiStyle.WarningOrange();
            }

            if (entity.GetComponent<NpcComponent>() is not null)
            {
                return UiStyle.ActiveGreen();
            }

            if (entity.Faction == Faction.Enemy)
            {
                return UiStyle.BloodRed();
            }
        }

        return world is WorldState state && state.GetItemsAt(position).Count > 0
            ? UiStyle.RarityUncommon()
            : Colors.Transparent;
    }

    private void DrawLegendSwatches()
    {
        const float swatchSize = 5f;
        var x = InnerPadding;
        var y = Size.Y - LegendHeight + 7f;
        foreach (var entry in Legend)
        {
            DrawRect(new Rect2(new Vector2(x, y), new Vector2(swatchSize, swatchSize)), entry.Color);
            x += entry.Label.Length <= 4 ? 39f : 52f;
            if (x > Size.X - 40f)
            {
                x = InnerPadding;
                y += 11f;
            }
        }
    }

    private static Color ResolveTileColor(IWorldState world, Roguelike.Core.Position position)
    {
        var visible = world.IsVisible(position);
        var explored = world.IsExplored(position);
        if (!visible && !explored)
        {
            return new Color(0.01f, 0.01f, 0.02f, 0.95f);
        }

        var baseColor = world.GetTile(position) switch
        {
            TileType.Floor => UiStyle.MapLineBlue(0.84f),
            TileType.Wall => new Color(0.08f, 0.07f, 0.06f, 0.95f),
            TileType.Door or TileType.LockedDoor => UiStyle.MapGold(0.95f),
            TileType.StairsDown => UiStyle.BrightGold(),
            TileType.StairsUp => UiStyle.BrightGold(),
            TileType.Water => new Color(0.12f, 0.38f, 0.75f, 1f),
            TileType.Lava => new Color(0.82f, 0.32f, 0.12f, 1f),
            TileType.Trap => UiStyle.MapTrapRed(),
            _ => new Color(0.08f, 0.08f, 0.1f, 1f),
        };

        return visible
            ? baseColor
            : new Color(baseColor.R * 0.45f, baseColor.G * 0.45f, baseColor.B * 0.45f, 0.9f);
    }

    private static string BuildLegendText()
        => "Floor Door Stairs Trap\n@ Player  E Enemy  N NPC  * Item  C Chest";
}

public sealed record MinimapLegendEntry(string Marker, string Label, Color Color);
