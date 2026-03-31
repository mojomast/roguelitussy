using System;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class Minimap : Control
{
    private const float OuterPadding = 16f;
    private const float InnerPadding = 10f;
    private const float DefaultSize = 220f;

    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public Minimap()
    {
        Name = "Minimap";
        Size = new Vector2(DefaultSize, DefaultSize);
        CustomMinimumSize = Size;
        ZIndex = 35;
        Refresh();
    }

    public bool MinimapEnabled { get; private set; } = true;

    public int VisibleTileCount { get; private set; }

    public int ExploredTileCount { get; private set; }

    public Roguelike.Core.Position PlayerWorldPosition { get; private set; } = Roguelike.Core.Position.Invalid;

    public string SummaryText { get; private set; } = "Minimap unavailable";

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

    public void Refresh()
    {
        UpdatePlacement();

        var world = _gameManager?.World;
        var active = MinimapEnabled && world is not null && _gameManager?.CurrentState != GameManager.GameState.MainMenu;
        Visible = active;

        VisibleTileCount = 0;
        ExploredTileCount = 0;
        PlayerWorldPosition = Roguelike.Core.Position.Invalid;

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

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.06f, 0.08f, 0.88f));

        var availableWidth = Math.Max(1f, Size.X - (InnerPadding * 2f));
        var availableHeight = Math.Max(1f, Size.Y - (InnerPadding * 2f));
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

        if (PlayerWorldPosition != Roguelike.Core.Position.Invalid)
        {
            var inset = MathF.Max(1f, cellSize * 0.2f);
            var playerRect = new Rect2(
                origin + new Vector2(PlayerWorldPosition.X * cellSize, PlayerWorldPosition.Y * cellSize) + new Vector2(inset, inset),
                new Vector2(MathF.Max(1f, cellSize - (inset * 2f)), MathF.Max(1f, cellSize - (inset * 2f))));
            DrawRect(playerRect, new Color(0.95f, 0.82f, 0.24f, 1f));
        }
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

        var viewportSize = GetParent() is not null && GetTree() is not null
            ? GetViewportRect().Size
            : new Vector2(1280f, 720f);
        Position = new Vector2(viewportSize.X - Size.X - OuterPadding, OuterPadding);
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
            TileType.Floor => new Color(0.46f, 0.46f, 0.5f, 1f),
            TileType.Wall => new Color(0.17f, 0.18f, 0.22f, 1f),
            TileType.Door => new Color(0.61f, 0.4f, 0.18f, 1f),
            TileType.StairsDown => new Color(0.24f, 0.52f, 0.85f, 1f),
            TileType.StairsUp => new Color(0.28f, 0.72f, 0.42f, 1f),
            TileType.Water => new Color(0.12f, 0.38f, 0.75f, 1f),
            TileType.Lava => new Color(0.82f, 0.32f, 0.12f, 1f),
            _ => new Color(0.08f, 0.08f, 0.1f, 1f),
        };

        return visible
            ? baseColor
            : new Color(baseColor.R * 0.45f, baseColor.G * 0.45f, baseColor.B * 0.45f, 0.9f);
    }
}