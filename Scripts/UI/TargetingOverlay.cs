using System;
using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class TargetingOverlay : Control
{
    private EventBus? _eventBus;
    private IWorldState? _world;
    private EntityId _actorId;
    private EntityId _itemInstanceId;
    private ItemTemplate? _itemTemplate;
    private AbilityTemplate? _ability;
    private bool _isItemTargeting;
    private readonly List<Position> _previewTiles = new();
    private Position _cursorPosition = new(-1, -1);

    public TargetingOverlay()
    {
        Name = "TargetingOverlay";
        Visible = false;
    }

    public bool IsActive { get; private set; }

    public Position CursorPosition => _cursorPosition;

    public IReadOnlyList<Position> PreviewTiles => _previewTiles;

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        _eventBus = eventBus;
    }

    public void EnterTargetingForItem(IWorldState world, EntityId actorId, EntityId itemInstanceId, ItemTemplate itemTemplate, AbilityTemplate ability)
    {
        Reset();
        _world = world;
        _actorId = actorId;
        _itemInstanceId = itemInstanceId;
        _itemTemplate = itemTemplate;
        _ability = ability;
        _isItemTargeting = true;

        var actor = world.GetEntity(actorId);
        _cursorPosition = actor?.Position ?? new(-1, -1);
        IsActive = true;
        Visible = true;
        _eventBus?.EmitTargetingModeEntered("item");
        UpdatePreview();
    }

    public void EnterTargetingForAbility(IWorldState world, EntityId actorId, AbilityTemplate ability)
    {
        Reset();
        _world = world;
        _actorId = actorId;
        _ability = ability;
        _isItemTargeting = false;

        var actor = world.GetEntity(actorId);
        _cursorPosition = actor?.Position ?? new(-1, -1);
        IsActive = true;
        Visible = true;
        _eventBus?.EmitTargetingModeEntered("ability");
        UpdatePreview();
    }

    public void MoveCursor(Position delta)
    {
        if (!IsActive || _world is null)
        {
            return;
        }

        var next = _cursorPosition + delta;
        if (!_world.InBounds(next))
        {
            return;
        }

        _cursorPosition = next;
        UpdatePreview();
    }

    public IAction? Confirm()
    {
        if (!IsActive || _world is null || _ability is null || _actorId == EntityId.Invalid)
        {
            return null;
        }

        if (!IsCurrentTargetValid())
        {
            return null;
        }

        IAction? action;
        if (_isItemTargeting && _itemTemplate is not null)
        {
            action = new UseItemAction(_actorId, _itemInstanceId, _itemTemplate, _ability, _cursorPosition);
        }
        else
        {
            action = new CastAbilityAction(_actorId, _ability, _cursorPosition);
        }

        _eventBus?.EmitTargetingModeExited("confirmed");
        _eventBus?.EmitPlayerActionSubmitted(action);
        Reset();
        return action;
    }

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        _eventBus?.EmitTargetingModeExited("cancelled");
        Reset();
    }

    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Up or Key.W:
                MoveCursor(new Position(0, -1));
                return true;
            case Key.Down or Key.S:
                MoveCursor(new Position(0, 1));
                return true;
            case Key.Left or Key.A:
                MoveCursor(new Position(-1, 0));
                return true;
            case Key.Right or Key.D:
                MoveCursor(new Position(1, 0));
                return true;
            case Key.Enter or Key.KpEnter:
                Confirm();
                return true;
            case Key.Escape:
                Cancel();
                return true;
            default:
                return false;
        }
    }

    private void Reset()
    {
        IsActive = false;
        Visible = false;
        _world = null;
        _actorId = EntityId.Invalid;
        _itemInstanceId = EntityId.Invalid;
        _itemTemplate = null;
        _ability = null;
        _isItemTargeting = false;
        _previewTiles.Clear();
        _cursorPosition = new(-1, -1);
    }

    private void UpdatePreview()
    {
        _previewTiles.Clear();
        if (_world is null || _ability is null)
        {
            return;
        }

        var valid = IsCurrentTargetValid();
        _previewTiles.AddRange(ComputePreviewTiles());
        _eventBus?.EmitTargetingCursorMoved(_cursorPosition, valid);
        _eventBus?.EmitTargetingPreviewChanged(_previewTiles, valid);
    }

    private bool IsCurrentTargetValid()
    {
        if (_world is null || _ability is null || _actorId == EntityId.Invalid || !_world.InBounds(_cursorPosition))
        {
            return false;
        }

        var action = new CastAbilityAction(_actorId, _ability, _cursorPosition);
        return action.Validate(_world) == ActionResult.Success;
    }

    private IReadOnlyList<Position> ComputePreviewTiles()
    {
        if (_world is null || _ability is null)
        {
            return Array.Empty<Position>();
        }

        var actor = _world.GetEntity(_actorId);
        var center = _cursorPosition;
        if (string.Equals(_ability.Targeting.Type, "self", StringComparison.OrdinalIgnoreCase) && actor is not null)
        {
            center = actor.Position;
        }

        switch (_ability.Targeting.Type.ToLowerInvariant())
        {
            case "self":
                return actor is null ? Array.Empty<Position>() : new[] { actor.Position };
            case "single":
            case "tile":
                return new[] { center };
            case "aoe_circle":
                var tiles = new List<Position>();
                var radius = _ability.Targeting.Radius;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var p = center.Offset(dx, dy);
                        if (_world.InBounds(p))
                        {
                            tiles.Add(p);
                        }
                    }
                }

                return tiles;
            default:
                return Array.Empty<Position>();
        }
    }
}
