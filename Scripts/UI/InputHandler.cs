using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class InputHandler : Node
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private bool _inputEnabled = true;

    public event System.Action? InventoryRequested;

    public event System.Action? CharacterSheetRequested;

    public event System.Action? PauseRequested;

    public event System.Action? MinimapToggleRequested;

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        _gameManager = gameManager;
        _eventBus = eventBus;
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    public bool HandleKey(Key key)
    {
        if (!_inputEnabled || _gameManager?.CurrentState != GameManager.GameState.Playing)
        {
            return false;
        }

        var world = _gameManager.World;
        if (world is null)
        {
            return false;
        }

        var playerId = world.Player.Id;
        return key switch
        {
            Key.Up or Key.W => Submit(UIActionFactory.CreateDirectionalAction(world, playerId, new Position(0, -1))),
            Key.Down or Key.S => Submit(UIActionFactory.CreateDirectionalAction(world, playerId, new Position(0, 1))),
            Key.Left or Key.A => Submit(UIActionFactory.CreateDirectionalAction(world, playerId, new Position(-1, 0))),
            Key.Right or Key.D => Submit(UIActionFactory.CreateDirectionalAction(world, playerId, new Position(1, 0))),
            Key.Space or Key.Period => Submit(UIActionFactory.CreateWaitAction(world, playerId)),
            Key.G => Submit(UIActionFactory.CreatePickupAction(world, playerId)),
            Key.Enter => Submit(UIActionFactory.CreateStairsAction(world, playerId)),
            Key.I => Raise(InventoryRequested),
            Key.C => Raise(CharacterSheetRequested),
            Key.Escape => Raise(PauseRequested),
            Key.Tab => Raise(MinimapToggleRequested),
            _ => false,
        };
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