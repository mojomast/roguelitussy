using System;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ExaminePanel : MenuBase
{
    private GameManager? _gameManager;
    private IContentDatabase? _content;
    private IWorldState? _world;
    private Position _cursorPosition = new(-1, -1);
    private string _description = string.Empty;

    public ExaminePanel()
    {
        Name = "ExaminePanel";
        Title = "Examine";
        Visible = false;
    }

    public bool IsActive => Visible;

    public Position CursorPosition => _cursorPosition;

    public string CurrentDescription => _description;

    public void Bind(GameManager? gameManager, IContentDatabase? content)
    {
        _gameManager = gameManager;
        _content = content ?? gameManager?.Content;
    }

    public override void Open()
    {
        _world = _gameManager?.World;
        _cursorPosition = _world?.Player?.Position ?? new Position(-1, -1);
        RefreshDescription();
        base.Open();
    }

    public override void Close()
    {
        base.Close();
        _world = null;
    }

    public bool Toggle()
    {
        if (Visible)
        {
            Close();
            return false;
        }

        Open();
        return true;
    }

    public override bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

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
            case Key.Escape:
            case Key.X:
                Close();
                return true;
            default:
                return false;
        }
    }

    public void MoveCursor(Position delta)
    {
        if (!Visible || _world is null)
        {
            return;
        }

        var next = _cursorPosition + delta;
        if (!CanSelect(next))
        {
            return;
        }

        _cursorPosition = next;
        RefreshDescription();
        RebuildMenuText();
    }

    protected override void ActivateSelected()
    {
    }

    protected override string BuildBodyText()
    {
        return _description;
    }

    protected override string BuildFooterText()
    {
        return "WASD/Arrows move cursor  X/Esc close";
    }

    protected override Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var width = Math.Min(520f, Math.Max(360f, viewportSize.X * 0.42f));
        var height = Math.Min(260f, Math.Max(180f, viewportSize.Y * 0.34f));
        return new Vector2(width, height);
    }

    private void RefreshDescription()
    {
        _description = BuildDescription();
    }

    private bool CanSelect(Position position)
    {
        if (_world is null || !_world.InBounds(position) || (!_world.IsVisible(position) && !_world.IsExplored(position)))
        {
            return false;
        }

        var player = _world.Player;
        return player is null || player.Position.ChebyshevTo(position) <= Math.Max(1, player.Stats.ViewRadius);
    }

    private string BuildDescription()
    {
        if (_world is null || !_world.InBounds(_cursorPosition))
        {
            return "No map cell selected.";
        }

        var visible = _world.IsVisible(_cursorPosition);
        var explored = _world.IsExplored(_cursorPosition);
        if (!visible && !explored)
        {
            return $"({_cursorPosition.X}, {_cursorPosition.Y})\nUnexplored darkness.";
        }

        var tile = DescribeTile(_world.GetTile(_cursorPosition));
        var text = $"({_cursorPosition.X}, {_cursorPosition.Y})\n{tile}";
        if (!visible)
        {
            return text + "\nExplored, but not currently visible.";
        }

        var entity = _world.GetEntityAt(_cursorPosition);
        if (entity is not null)
        {
            var entityDescription = DescribeEntity(entity);
            if (!string.IsNullOrWhiteSpace(entityDescription))
            {
                text += "\n" + entityDescription;
            }
        }

        var items = _world is WorldState state ? state.GetItemsAt(_cursorPosition) : Array.Empty<ItemInstance>();
        if (items.Count > 0)
        {
            text += "\n" + DescribeItems(items);
        }

        return text;
    }

    private string? DescribeEntity(IEntity entity)
    {
        if (entity.GetComponent<ChestComponent>() is { } chest)
        {
            return $"Chest. Loot table: {chest.LootTableId}.";
        }

        if (entity.GetComponent<TrapComponent>() is { } trap)
        {
            if (!trap.IsRevealed)
            {
                return null;
            }

            var trapName = _content?.TryGetTrapTemplate(trap.TemplateId, out var trapTemplate) == true
                ? trapTemplate.DisplayName
                : "Trap";
            var state = trap.IsArmed ? "armed" : "spent";
            return $"{trapName}. {state}.";
        }

        if (entity.GetComponent<NpcComponent>() is { } npc && _content?.TryGetNpcTemplate(npc.TemplateId, out var npcTemplate) == true)
        {
            return $"{npcTemplate.DisplayName}. {npcTemplate.Description}";
        }

        if (entity.GetComponent<EnemyComponent>() is { } enemy && _content?.TryGetEnemyTemplate(enemy.TemplateId, out var enemyTemplate) == true)
        {
            return $"{enemyTemplate.DisplayName}. {enemyTemplate.Description} HP {entity.Stats.HP}/{entity.Stats.MaxHP}.";
        }

        return $"{entity.Name}. HP {entity.Stats.HP}/{entity.Stats.MaxHP}.";
    }

    private string DescribeItems(System.Collections.Generic.IReadOnlyList<ItemInstance> items)
    {
        var names = items.Take(3).Select(item =>
        {
            var name = _content?.TryGetItemTemplate(item.TemplateId, out var template) == true
                ? template.DisplayName
                : item.TemplateId;
            return item.StackCount > 1 ? $"{item.StackCount}x {name}" : name;
        }).ToArray();
        var suffix = items.Count > names.Length ? $", +{items.Count - names.Length} more" : string.Empty;
        return "Items: " + string.Join(", ", names) + suffix + ".";
    }

    private string DescribeTile(TileType tile)
    {
        if (_world is WorldState state && tile == TileType.Door)
        {
            return state.IsDoorOpen(_cursorPosition) ? "Open door." : "Closed door.";
        }

        return tile switch
        {
            TileType.Void => "Unmapped void.",
            TileType.Wall => "Stone wall.",
            TileType.Floor => "Dungeon floor.",
            TileType.Door => "Closed door.",
            TileType.LockedDoor => "Locked door.",
            TileType.StairsDown => "Stairs leading down.",
            TileType.StairsUp => "Stairs leading up.",
            TileType.Water => "Shallow water.",
            TileType.Lava => "Molten lava.",
            TileType.Trap => "Trap tile.",
            _ => tile.ToString(),
        };
    }
}
