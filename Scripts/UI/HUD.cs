using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class HUD : Control
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public string HPText { get; private set; } = "HP: --/--";

    public string EnergyText { get; private set; } = "Energy: --";

    public string FloorText { get; private set; } = "Floor: --";

    public string TurnText { get; private set; } = "Turn: --";

    public string StatusEffectsText { get; private set; } = string.Empty;

    public string MinimapText { get; private set; } = "Map hidden";

    public Color HPColor { get; private set; } = Colors.White;

    public bool MinimapVisible { get; private set; } = true;

    public HUD()
    {
        Name = "HUD";
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.HPChanged -= OnHPChanged;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.FloorChanged += OnFloorChanged;
            _eventBus.HPChanged += OnHPChanged;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        Refresh();
    }

    public void ToggleMinimap()
    {
        MinimapVisible = !MinimapVisible;
        Refresh();
    }

    public string Snapshot()
    {
        var builder = new StringBuilder();
        builder.AppendLine(HPText);
        builder.AppendLine(EnergyText);
        builder.AppendLine(FloorText);
        builder.AppendLine(TurnText);
        if (!string.IsNullOrWhiteSpace(StatusEffectsText))
        {
            builder.AppendLine(StatusEffectsText);
        }

        builder.Append(MinimapText);
        return builder.ToString().TrimEnd();
    }

    private void OnTurnCompleted()
    {
        Refresh();
    }

    private void OnFloorChanged(int floor)
    {
        FloorText = $"Floor: {floor}";
        Refresh();
    }

    private void OnHPChanged(EntityId entityId, int currentHp, int maxHp)
    {
        if (_gameManager?.World?.Player.Id == entityId)
        {
            HPText = $"HP: {currentHp}/{maxHp}";
            UpdateHPColor(currentHp, maxHp);
            Refresh();
        }
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var world = _gameManager?.World;
        if (world is null)
        {
            return;
        }

        var player = world.Player;
        if (player is null)
        {
            HPText = "HP: --/--";
            EnergyText = "Energy: --";
            FloorText = $"Floor: {world.Depth}";
            TurnText = $"Turn: {world.TurnNumber}";
            StatusEffectsText = string.Empty;
            MinimapText = MinimapVisible
                ? $"Minimap: 0 explored, 0 visible"
                : "Minimap hidden";
            HPColor = Colors.White;
            return;
        }

        HPText = $"HP: {player.Stats.HP}/{player.Stats.MaxHP}";
        UpdateHPColor(player.Stats.HP, player.Stats.MaxHP);

        var energy = _gameManager?.Scheduler is TurnScheduler scheduler
            ? scheduler.GetEnergy(player.Id)
            : player.Stats.Energy;
        EnergyText = $"Energy: {energy}";
        FloorText = $"Floor: {world.Depth}";
        TurnText = $"Turn: {world.TurnNumber}";

        var effects = StatusEffectProcessor.GetEffects(player);
        StatusEffectsText = effects.Count == 0
            ? string.Empty
            : "Effects: " + string.Join(", ", effects.Select(effect => $"{effect.Type}({effect.RemainingTurns})"));

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

        MinimapText = MinimapVisible
            ? $"Minimap: {exploredTiles} explored, {visibleTiles} visible"
            : "Minimap hidden";
    }

    private void UpdateHPColor(int currentHp, int maxHp)
    {
        var ratio = maxHp <= 0 ? 0f : (float)currentHp / maxHp;
        HPColor = ratio switch
        {
            > 0.6f => Colors.Green,
            > 0.3f => Colors.Yellow,
            _ => Colors.Red,
        };
    }
}