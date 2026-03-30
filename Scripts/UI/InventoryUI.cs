using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class InventoryUI : Control
{
    private const int Columns = 5;
    private const int Rows = 4;
    private readonly List<ItemInstance> _items = new();
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IContentDatabase? _content;
    private Tooltip? _tooltip;

    public int SelectedIndex { get; private set; }

    public string GridText { get; private set; } = string.Empty;

    public string DescriptionText { get; private set; } = string.Empty;

    public InventoryUI()
    {
        Name = "InventoryUI";
        Visible = false;
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content, Tooltip? tooltip)
    {
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
        _tooltip = tooltip;
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        RefreshFromWorld();
    }

    public void Open()
    {
        Visible = true;
        RefreshFromWorld();
        SelectedIndex = 0;
        UpdateDescription();
        UpdateGrid();
    }

    public void Close()
    {
        Visible = false;
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
            return;
        }

        Open();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
                MoveSelection(0, -1);
                return true;
            case Key.Down:
                MoveSelection(0, 1);
                return true;
            case Key.Left:
                MoveSelection(-1, 0);
                return true;
            case Key.Right:
                MoveSelection(1, 0);
                return true;
            case Key.U:
            case Key.Enter:
            case Key.E:
                SubmitUse();
                return true;
            case Key.D:
                SubmitDrop();
                return true;
            case Key.I:
            case Key.Escape:
                Close();
                _tooltip?.Hide();
                return true;
            default:
                return false;
        }
    }

    private void OnInventoryChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player.Id == entityId)
        {
            RefreshFromWorld();
        }
    }

    private void OnTurnCompleted()
    {
        RefreshFromWorld();
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            RefreshFromWorld();
        }
    }

    private void RefreshFromWorld()
    {
        _items.Clear();
        var inventory = _gameManager?.World?.Player.GetComponent<InventoryComponent>();
        if (inventory is not null)
        {
            _items.AddRange(inventory.Items);
        }

        if (SelectedIndex >= Columns * Rows)
        {
            SelectedIndex = 0;
        }

        UpdateDescription();
        UpdateGrid();
    }

    private void MoveSelection(int dx, int dy)
    {
        var column = SelectedIndex % Columns;
        var row = SelectedIndex / Columns;

        column = (column + dx + Columns) % Columns;
        row = (row + dy + Rows) % Rows;

        SelectedIndex = (row * Columns) + column;
        UpdateDescription();
        UpdateGrid();
    }

    private void SubmitUse()
    {
        if (SelectedIndex >= _items.Count || _eventBus is null)
        {
            return;
        }

        var playerId = _gameManager?.World?.Player.Id ?? EntityId.Invalid;
        var action = UIActionFactory.CreateUseItemAction(_gameManager?.World, _content, playerId, _items[SelectedIndex].InstanceId);
        if (action is not null)
        {
            _eventBus.EmitPlayerActionSubmitted(action);
            Close();
        }
    }

    private void SubmitDrop()
    {
        if (SelectedIndex >= _items.Count || _eventBus is null)
        {
            return;
        }

        var playerId = _gameManager?.World?.Player.Id ?? EntityId.Invalid;
        var action = UIActionFactory.CreateDropItemAction(_gameManager?.World, playerId, _items[SelectedIndex].InstanceId);
        if (action is not null)
        {
            _eventBus.EmitPlayerActionSubmitted(action);
            Close();
        }
    }

    private void UpdateGrid()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Inventory");
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var slotIndex = (row * Columns) + column;
                var selected = slotIndex == SelectedIndex;
                var label = slotIndex < _items.Count ? ResolveSlotGlyph(_items[slotIndex]) : " ";
                builder.Append(selected ? ">" : " ");
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
            }

            builder.AppendLine();
        }

        builder.Append("Use: U/Enter/E  Drop: D  Close: I/Esc");
        GridText = builder.ToString().TrimEnd();
    }

    private void UpdateDescription()
    {
        if (SelectedIndex >= _items.Count)
        {
            DescriptionText = "Empty slot";
            _tooltip?.Hide();
            return;
        }

        var item = _items[SelectedIndex];
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var lines = new List<string>
            {
                template.DisplayName,
                template.Description,
            };

            foreach (var modifier in template.StatModifiers)
            {
                lines.Add($"{modifier.Key}: {modifier.Value:+#;-#;0}");
            }

            DescriptionText = string.Join("\n", lines);
            _tooltip?.ShowItemTooltip(template, item, new Vector2(840f, 220f));
            return;
        }

        DescriptionText = item.TemplateId;
        _tooltip?.Hide();
    }

    private string ResolveSlotGlyph(ItemInstance item)
    {
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template) && !string.IsNullOrWhiteSpace(template.DisplayName))
        {
            return template.DisplayName[..1].ToUpperInvariant();
        }

        return item.TemplateId[..1].ToUpperInvariant();
    }
}