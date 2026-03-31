using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class InventoryUI : Control
{
    private enum SortMode
    {
        Equipped,
        Category,
        Name,
    }

    private const int Columns = 5;
    private const int Rows = 4;
    private readonly List<ItemInstance> _items = new();
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IContentDatabase? _content;
    private Tooltip? _tooltip;
    private Panel? _panel;
    private Label? _gridLabel;
    private Label? _descriptionLabel;

    private const float PanelWidth = 960f;
    private const float PanelHeight = 420f;
    private const float PanelPadding = 18f;

    public int SelectedIndex { get; private set; }

    public string SortLabel => CurrentSort.ToString();

    public string GridText { get; private set; } = string.Empty;

    public string DescriptionText { get; private set; } = string.Empty;

    private SortMode CurrentSort { get; set; } = SortMode.Equipped;

    public InventoryUI()
    {
        Name = "InventoryUI";
        Visible = false;
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
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
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        _tooltip?.Hide();
        RefreshVisualState();
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
            case Key.Enter:
                SubmitPrimary();
                return true;
            case Key.U:
                SubmitUse();
                return true;
            case Key.E:
                SubmitEquip();
                return true;
            case Key.D:
                SubmitDrop();
                return true;
            case Key.Tab:
                CycleSortMode();
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
        if (_gameManager?.World?.Player?.Id == entityId)
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
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        if (inventory is not null)
        {
            _items.AddRange(inventory.Items);
        }

        SortItems();

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
        if (!TryGetSelectedItem(out var item) || _eventBus is null)
        {
            return;
        }

        var playerId = _gameManager?.World?.Player.Id ?? EntityId.Invalid;
        var action = UIActionFactory.CreateUseItemAction(_gameManager?.World, _content, playerId, item.InstanceId);
        if (action is not null)
        {
            _eventBus.EmitPlayerActionSubmitted(action);
            Close();
        }
    }

    private void SubmitEquip()
    {
        if (!TryGetSelectedItem(out var item) || _eventBus is null)
        {
            return;
        }

        var playerId = _gameManager?.World?.Player.Id ?? EntityId.Invalid;
        var action = UIActionFactory.CreateToggleEquipAction(_gameManager?.World, _content, playerId, item.InstanceId);
        if (action is not null)
        {
            _eventBus.EmitPlayerActionSubmitted(action);
            Close();
        }
    }

    private void SubmitDrop()
    {
        if (!TryGetSelectedItem(out var item) || _eventBus is null)
        {
            return;
        }

        var playerId = _gameManager?.World?.Player.Id ?? EntityId.Invalid;
        var quantity = item.StackCount > 1 ? 1 : int.MaxValue;
        var action = UIActionFactory.CreateDropItemAction(_gameManager?.World, playerId, item.InstanceId, quantity);
        if (action is not null)
        {
            _eventBus.EmitPlayerActionSubmitted(action);
            Close();
        }
    }

    private void SubmitPrimary()
    {
        if (!TryGetSelectedItem(out var item))
        {
            return;
        }

        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template) && template.Slot != EquipSlot.None)
        {
            SubmitEquip();
            return;
        }

        SubmitUse();
    }

    private void UpdateGrid()
    {
        var builder = new StringBuilder();
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        builder.AppendLine($"Inventory  {_items.Count}/{inventory?.Capacity ?? 0}");
        builder.AppendLine($"Sort: {CurrentSort}");
        builder.AppendLine();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var slotIndex = (row * Columns) + column;
                var selected = slotIndex == SelectedIndex;
                var label = slotIndex < _items.Count ? ResolveSlotToken(_items[slotIndex]) : "   ";
                builder.Append(selected ? ">" : " ");
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
            }

            builder.AppendLine();
        }

        builder.Append("Use: U  Equip: E/Enter  Drop: D  Sort: Tab  Close: I/Esc");
        GridText = builder.ToString().TrimEnd();
        RefreshVisualState();
    }

    private void UpdateDescription()
    {
        if (SelectedIndex >= _items.Count)
        {
            DescriptionText = "Empty slot\n\nCycle sort with Tab to reorganize the bag.";
            _tooltip?.Hide();
            RefreshVisualState();
            return;
        }

        var item = _items[SelectedIndex];
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var lines = new List<string>
            {
                template.DisplayName,
                template.Description,
                $"Category: {template.Category}",
                $"Slot: {(template.Slot == EquipSlot.None ? "None" : template.Slot.ToString())}",
                $"Stack: {item.StackCount}",
                $"Status: {(IsEquipped(item) ? $"Equipped in {ResolveEquippedSlot(item)}" : "Carried")}",
            };

            foreach (var modifier in template.StatModifiers)
            {
                lines.Add($"{modifier.Key}: {modifier.Value:+#;-#;0}");
            }

            lines.Add(template.Slot == EquipSlot.None
                ? "U/Enter: use item"
                : (IsEquipped(item) ? "E/Enter: unequip" : "E/Enter: equip"));
            lines.Add(item.StackCount > 1 ? "D: drop one from stack" : "D: drop item");

            DescriptionText = string.Join("\n", lines);
            _tooltip?.ShowItemTooltip(template, item, new Vector2(840f, 220f));
            RefreshVisualState();
            return;
        }

        DescriptionText = item.TemplateId;
        _tooltip?.Hide();
        RefreshVisualState();
    }

    private void CycleSortMode()
    {
        CurrentSort = CurrentSort switch
        {
            SortMode.Equipped => SortMode.Category,
            SortMode.Category => SortMode.Name,
            _ => SortMode.Equipped,
        };

        SortItems();
        UpdateDescription();
        UpdateGrid();
    }

    private void SortItems()
    {
        _items.Sort(CompareItems);
    }

    private int CompareItems(ItemInstance left, ItemInstance right)
    {
        if (CurrentSort == SortMode.Equipped)
        {
            var equippedCompare = CompareFalseFirst(IsEquipped(left), IsEquipped(right));
            if (equippedCompare != 0)
            {
                return equippedCompare;
            }
        }

        if (CurrentSort is SortMode.Equipped or SortMode.Category)
        {
            var categoryCompare = GetCategoryRank(left).CompareTo(GetCategoryRank(right));
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }
        }

        return string.Compare(ResolveDisplayName(left), ResolveDisplayName(right), System.StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareFalseFirst(bool left, bool right)
    {
        if (left == right)
        {
            return 0;
        }

        return left ? -1 : 1;
    }

    private int GetCategoryRank(ItemInstance item)
    {
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return (int)template.Category;
        }

        return int.MaxValue;
    }

    private string ResolveSlotToken(ItemInstance item)
    {
        var marker = IsEquipped(item) ? "*" : string.Empty;
        var glyph = ResolveSlotGlyph(item);
        var count = item.StackCount > 1 ? System.Math.Min(item.StackCount, 9).ToString() : string.Empty;
        var token = $"{marker}{glyph}{count}";
        return token.PadRight(3);
    }

    private string ResolveSlotGlyph(ItemInstance item)
    {
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template) && !string.IsNullOrWhiteSpace(template.DisplayName))
        {
            return template.DisplayName[..1].ToUpperInvariant();
        }

        return item.TemplateId[..1].ToUpperInvariant();
    }

    private bool IsEquipped(ItemInstance item)
    {
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        return inventory?.IsEquipped(item.InstanceId) == true;
    }

    private string ResolveEquippedSlot(ItemInstance item)
    {
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        var slot = inventory?.GetEquippedSlot(item.InstanceId) ?? EquipSlot.None;
        return slot == EquipSlot.None ? "None" : slot.ToString();
    }

    private string ResolveDisplayName(ItemInstance item)
    {
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return template.DisplayName;
        }

        return item.TemplateId;
    }

    private bool TryGetSelectedItem(out ItemInstance item)
    {
        if (SelectedIndex < 0 || SelectedIndex >= _items.Count)
        {
            item = default!;
            return false;
        }

        item = _items[SelectedIndex];
        return true;
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _gridLabel is not null && _descriptionLabel is not null)
        {
            return;
        }

        Size = ResolveViewportSize();
        ZIndex = 95;
        _panel = new Panel
        {
            Name = "Panel",
            Size = new Vector2(PanelWidth, PanelHeight),
        };
        _gridLabel = new Label
        {
            Name = "GridLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(420f, PanelHeight - (PanelPadding * 2f)),
        };
        _descriptionLabel = new Label
        {
            Name = "DescriptionLabel",
            Position = new Vector2(460f, PanelPadding),
            Size = new Vector2(PanelWidth - 478f, PanelHeight - (PanelPadding * 2f)),
        };
        _panel.AddChild(_gridLabel);
        _panel.AddChild(_descriptionLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _gridLabel is null || _descriptionLabel is null)
        {
            return;
        }

        Size = ResolveViewportSize();
        _panel.Position = new Vector2((Size.X - _panel.Size.X) * 0.5f, (Size.Y - _panel.Size.Y) * 0.5f);
        _panel.Visible = Visible;
        _gridLabel.Visible = Visible;
        _descriptionLabel.Visible = Visible;
        _gridLabel.Text = GridText;
        _descriptionLabel.Text = DescriptionText;
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}