using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class InputHandler : Node
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private bool _inputEnabled = true;
    private bool _runPrefixActive;
    private bool _diagonalPrefixActive;
    private Position _diagonalDirection;
    private IWorldState? _inputWorld;

    public event System.Action? InventoryRequested;

    public event System.Action? CharacterSheetRequested;

    public event System.Action? PauseRequested;

    public event System.Action? MinimapToggleRequested;

    public event System.Action? MinimapLegendToggleRequested;

    public event System.Action? HelpRequested;

    public event System.Action? ToolsRequested;

    public event System.Action? InteractRequested;

    public event System.Action? ExamineRequested;

    public event System.Action? CombatLogFilterCycleRequested;

    public event System.Action? AbilitiesRequested;

    public bool IsRunPrefixActive => _runPrefixActive;

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        _gameManager = gameManager;
        _eventBus = eventBus;
        _inputWorld = gameManager?.World;
        ClearPrefixes();
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled)
        {
            ClearPrefixes();
        }
    }

    public bool HandleKey(Key key)
    {
        var world = _gameManager?.World;
        if (!ReferenceEquals(_inputWorld, world))
        {
            ClearPrefixes();
            _inputWorld = world;
        }

        if (!_inputEnabled || _gameManager?.CurrentState != GameManager.GameState.Playing)
        {
            ClearPrefixes();
            return false;
        }

        if (world is null)
        {
            return false;
        }

        if (world.Player is null)
        {
            return false;
        }

        var playerId = world.Player.Id;
        if (_diagonalPrefixActive)
        {
            if (key == Key.V || key == Key.Escape)
            {
                ClearPrefixes();
                _eventBus?.EmitLogMessage("Diagonal cancelled.", LogCategory.System);
                return true;
            }

            if (TryGetDirection(key, out var direction))
            {
                if (_diagonalDirection == Position.Zero || (_diagonalDirection.X == 0) == (direction.X == 0))
                {
                    _diagonalDirection = direction;
                    _eventBus?.EmitLogMessage("Diagonal: choose a perpendicular arrow/WASD direction; V or Escape cancels.", LogCategory.System);
                    return true;
                }

                var delta = _diagonalDirection + direction;
                ClearPrefixes();
                return HandleDirectionalInput(world, playerId, delta);
            }

            ClearPrefixes();
        }

        if (_runPrefixActive)
        {
            if (TryGetDirection(key, out var runDelta))
            {
                _runPrefixActive = false;
                var steps = _gameManager.RunPlayerUntilBlocked(runDelta);
                if (steps == 0)
                {
                    _eventBus?.EmitLogMessage("Run blocked.", LogCategory.System);
                }

                return true;
            }

            if (key == Key.Escape || key == Key.R)
            {
                _runPrefixActive = false;
                _eventBus?.EmitLogMessage("Run cancelled.", LogCategory.System);
                return true;
            }

            return false;
        }

        return key switch
        {
            Key.Up or Key.W or Key.Kp8 => HandleDirectionalInput(world, playerId, new Position(0, -1)),
            Key.Down or Key.S or Key.Kp2 => HandleDirectionalInput(world, playerId, new Position(0, 1)),
            Key.Left or Key.A or Key.Kp4 => HandleDirectionalInput(world, playerId, new Position(-1, 0)),
            Key.Right or Key.D or Key.Kp6 => HandleDirectionalInput(world, playerId, new Position(1, 0)),
            Key.Home or Key.Kp7 => HandleDirectionalInput(world, playerId, new Position(-1, -1)),
            Key.Pageup or Key.Kp9 => HandleDirectionalInput(world, playerId, new Position(1, -1)),
            Key.End or Key.Kp1 => HandleDirectionalInput(world, playerId, new Position(-1, 1)),
            Key.Pagedown or Key.Kp3 => HandleDirectionalInput(world, playerId, new Position(1, 1)),
            Key.V => EnterDiagonalPrefix(),
            Key.R => EnterRunPrefix(),
            Key.O => AutoExplore(),
            Key.Z => RestUntilHealed(),
            Key.Space or Key.Period or Key.Kp5 => Submit(UIActionFactory.CreateWaitAction(world, playerId)),
            Key.G => Submit(UIActionFactory.CreatePickupAction(world, _gameManager?.Content, playerId, _gameManager?.AutoEquipUpgradesEnabled == true)),
            Key.Enter or Key.KpEnter => Submit(UIActionFactory.CreateStairsAction(world, playerId)),
            Key.Key1 => HandleQuickUse(world, playerId, 0),
            Key.Key2 => HandleQuickUse(world, playerId, 1),
            Key.Key3 => HandleQuickUse(world, playerId, 2),
            Key.Key4 => HandleQuickUse(world, playerId, 3),
            Key.Key5 => HandleQuickUse(world, playerId, 4),
            Key.I => Raise(InventoryRequested),
            Key.B => Raise(AbilitiesRequested),
            Key.C => Raise(CharacterSheetRequested),
            Key.H => Raise(HelpRequested),
            Key.L => Raise(CombatLogFilterCycleRequested),
            Key.F => Raise(InteractRequested),
            Key.T => Raise(ToolsRequested),
            Key.X => Raise(ExamineRequested),
            Key.Escape => Raise(PauseRequested),
            Key.M or Key.Tab => Raise(MinimapToggleRequested),
            Key.U => Raise(MinimapLegendToggleRequested),
            _ => false,
        };
    }

    private void ClearPrefixes()
    {
        _runPrefixActive = false;
        _diagonalPrefixActive = false;
        _diagonalDirection = Position.Zero;
    }

    private bool EnterDiagonalPrefix()
    {
        _diagonalPrefixActive = true;
        _diagonalDirection = Position.Zero;
        _eventBus?.EmitLogMessage("Diagonal: choose two perpendicular arrows/WASD (e.g. Up then Right). V or Escape cancels; choosing spends no turn.", LogCategory.System);
        return true;
    }

    private bool EnterRunPrefix()
    {
        _runPrefixActive = true;
        _eventBus?.EmitLogMessage("Run: choose a direction, or Escape to cancel.", LogCategory.System);
        return true;
    }

    private bool RestUntilHealed()
    {
        _eventBus?.EmitLogMessage("Resting until healed or interrupted.", LogCategory.System);
        var turns = _gameManager?.RestPlayerUntilHealed() ?? 0;
        _eventBus?.EmitLogMessage(turns == 0 ? "Rest stopped." : $"Rest stopped after {turns} turns.", LogCategory.System);

        return true;
    }

    private bool AutoExplore()
    {
        _eventBus?.EmitLogMessage("Autoexploring until interrupted.", LogCategory.System);
        var steps = _gameManager?.AutoExplorePlayer() ?? 0;
        _eventBus?.EmitLogMessage(steps == 0 ? "Autoexplore stopped." : $"Autoexplore stopped after {steps} steps.", LogCategory.System);

        return true;
    }

    private static bool TryGetDirection(Key key, out Position delta)
    {
        delta = key switch
        {
            Key.Up or Key.W => new Position(0, -1),
            Key.Down or Key.S => new Position(0, 1),
            Key.Left or Key.A => new Position(-1, 0),
            Key.Right or Key.D => new Position(1, 0),
            _ => Position.Zero,
        };

        return delta != Position.Zero;
    }

    private bool HandleDirectionalInput(IWorldState world, EntityId playerId, Position delta)
    {
        var actor = world.GetEntity(playerId);
        if (actor is not null && world.GetEntityAt(actor.Position + delta)?.GetComponent<ChestComponent>() is not null)
        {
            return Raise(InteractRequested);
        }

        return Submit(UIActionFactory.CreateDirectionalAction(world, playerId, delta));
    }

    private bool HandleQuickUse(IWorldState world, EntityId playerId, int slotIndex)
    {
        var content = _gameManager?.Content;
        var items = UIActionFactory.GetQuickUseItems(world, content, playerId);
        if (slotIndex < 0 || slotIndex >= items.Count)
        {
            _eventBus?.EmitLogMessage($"Quick slot {slotIndex + 1} is empty.", LogCategory.Warning);
            return true;
        }

        var item = items[slotIndex];
        if (content is not null
            && content.TryGetItemTemplate(item.TemplateId, out var template)
            && template.RequiresTargetSelection)
        {
            _eventBus?.EmitLogMessage($"{template.DisplayName} needs targeting. Open inventory and aim it from there.", LogCategory.Warning);
            return true;
        }

        var action = UIActionFactory.CreateUseItemAction(world, content, playerId, item.InstanceId);
        if (action is null)
        {
            _eventBus?.EmitLogMessage($"Quick slot {slotIndex + 1} cannot be used right now.", LogCategory.Warning);
            return true;
        }

        return Submit(action);
    }

    private bool Submit(IAction? action)
    {
        if (action is null || _eventBus is null)
        {
            return false;
        }

        _eventBus.EmitPlayerActionSubmitted(action);
        return true;
    }

    private static bool Raise(System.Action? callback)
    {
        if (callback is null)
        {
            return false;
        }

        callback();
        return true;
    }
}
