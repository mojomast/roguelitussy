using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class DebugOverlay : Control
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public DebugOverlay()
    {
        Name = "DebugOverlay";
        Visible = false;
        OverlayText = "Debug overlay inactive.";
        CustomMinimumSize = new Vector2(320f, 240f);
    }

    public string OverlayText { get; private set; }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= Refresh;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.EntitySpawned -= OnEntityChanged;
            _eventBus.EntityRemoved -= OnEntityRemoved;
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.HPChanged -= OnHpChanged;
            _eventBus.FovRecalculated -= Refresh;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += Refresh;
            _eventBus.FloorChanged += OnFloorChanged;
            _eventBus.EntitySpawned += OnEntityChanged;
            _eventBus.EntityRemoved += OnEntityRemoved;
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.HPChanged += OnHpChanged;
            _eventBus.FovRecalculated += Refresh;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        Refresh();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        var world = _gameManager?.World;
        if (world is null)
        {
            OverlayText = "Debug Overlay\nWorld: not loaded";
            return;
        }

        var player = world.Player;
        if (player is null)
        {
            OverlayText = $"Debug Overlay\nState: {_gameManager?.CurrentState}\nWorld: no active run\nFloor: {world.Depth}\nTurn: {world.TurnNumber}";
            return;
        }

        var inventoryCount = player.GetComponent<InventoryComponent>()?.Items.Count ?? 0;
        var visibleTiles = 0;
        var exploredTiles = 0;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.IsVisible(position))
                {
                    visibleTiles++;
                }

                if (world.IsExplored(position))
                {
                    exploredTiles++;
                }
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine("Debug Overlay");
        builder.AppendLine($"State: {_gameManager?.CurrentState}");
        builder.AppendLine($"Floor: {world.Depth}");
        builder.AppendLine($"Turn: {world.TurnNumber}");
        builder.AppendLine($"Player HP: {player.Stats.HP}/{player.Stats.MaxHP}");
        builder.AppendLine($"Player Pos: {player.Position.X},{player.Position.Y}");
        builder.AppendLine($"Entities: {world.Entities.Count}");
        builder.AppendLine($"Ground Items: {world.GetGroundItems().Sum(pair => pair.Value.Count)}");
        builder.AppendLine($"Inventory: {inventoryCount}");
        builder.AppendLine($"Visible Tiles: {visibleTiles}");
        builder.Append($"Explored Tiles: {exploredTiles}");
        OverlayText = builder.ToString();
    }

    private void OnFloorChanged(int floor)
    {
        Refresh();
    }

    private void OnEntityChanged(IEntity entity)
    {
        Refresh();
    }

    private void OnEntityRemoved(EntityId entityId)
    {
        Refresh();
    }

    private void OnInventoryChanged(EntityId entityId)
    {
        Refresh();
    }

    private void OnHpChanged(EntityId entityId, int currentHp, int maxHp)
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
}