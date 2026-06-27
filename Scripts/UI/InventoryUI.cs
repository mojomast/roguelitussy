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
    private Tooltip? _activeTooltip;
    private TargetingOverlay? _targetingOverlay;
    private Panel? _panel;
    private ColorRect? _panelBackground;
    private ColorRect? _panelBorderTop;
    private ColorRect? _panelBorderBottom;
    private ColorRect? _panelBorderLeft;
    private ColorRect? _panelBorderRight;
    private ColorRect? _headerBar;
    private Label? _headerTitleLabel;
    private Label? _headerStatsLabel;
    private Label? _sortLabel;
    private ColorRect? _footerBar;
    private readonly List<Label> _footerHintLabels = new();
    private readonly List<ColorRect> _footerDividers = new();
    private readonly List<SlotVisual> _slotVisuals = new();
    private RichTextLabel? _gridLabel;
    private RichTextLabel? _descriptionLabel;
    private int _firstVisibleIndex;

    private const float PanelWidth = 960f;
    private const float PanelHeight = 420f;
    private const float PanelPadding = 18f;
    private const float OuterMargin = 24f;
    private const float HeaderHeight = 28f;
    private const float FooterHeight = 22f;
    private const float SlotSize = 52f;
    private const float SlotGap = 8f;

    private sealed class SlotVisual
    {
        public InteractiveRect Background { get; init; } = null!;
        public ColorRect BorderTop { get; init; } = null!;
        public ColorRect BorderBottom { get; init; } = null!;
        public ColorRect BorderLeft { get; init; } = null!;
        public ColorRect BorderRight { get; init; } = null!;
        public Label Glyph { get; init; } = null!;
        public ColorRect StackBadge { get; init; } = null!;
        public Label StackLabel { get; init; } = null!;
        public ColorRect EquippedBadge { get; init; } = null!;
        public Label EquippedLabel { get; init; } = null!;
    }

    private sealed class InteractiveRect : ColorRect
    {
        public int Index { get; init; }
        public System.Action<int, InputEvent>? InputSubmitted { get; init; }

        public override void _GuiInput(InputEvent @event)
        {
            InputSubmitted?.Invoke(Index, @event);
        }
    }

    private sealed class FooterHintLabel : Label
    {
        public System.Action? Activated { get; init; }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Activated?.Invoke();
            }
        }
    }

    public int SelectedIndex { get; private set; }

    public string SortLabel => CurrentSort.ToString();

    public string GridText { get; private set; } = string.Empty;

    public string GridMarkup { get; private set; } = string.Empty;

    public string DescriptionText { get; private set; } = string.Empty;

    public string DescriptionMarkup { get; private set; } = string.Empty;

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

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content, Tooltip? tooltip, TargetingOverlay? targetingOverlay = null)
    {
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
        _tooltip = tooltip;
        _targetingOverlay = targetingOverlay;
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.LoadCompleted += OnLoadCompleted;
            _eventBus.CurrencyChanged += OnCurrencyChanged;
        }

        RefreshFromWorld();
    }

    public void Open()
    {
        Visible = true;
        RefreshFromWorld();
        SelectedIndex = 0;
        _firstVisibleIndex = 0;
        UpdateDescription();
        UpdateGrid();
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        HideActiveTooltip();
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
            case Key.KpEnter:
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
            case Key.A:
                ToggleAutoEquipUpgrades();
                return true;
            case Key.Tab:
                CycleSortMode();
                return true;
            case Key.I:
            case Key.Escape:
                Close();
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

    private void OnCurrencyChanged(EntityId entityId, int gold)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
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

        if (SelectedIndex >= _items.Count)
        {
            SelectedIndex = 0;
        }

        EnsureSelectionVisible();

        UpdateDescription();
        UpdateGrid();
    }

    private void MoveSelection(int dx, int dy)
    {
        var pageSize = Columns * Rows;
        var selectableCount = System.Math.Max(pageSize, _items.Count);
        SelectedIndex = (SelectedIndex + dx + (dy * Columns) + selectableCount) % selectableCount;
        EnsureSelectionVisible();
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
        if (_content is not null
            && _content.TryGetItemTemplate(item.TemplateId, out var template)
            && template.RequiresTargetSelection)
        {
            var world = _gameManager?.World;
            if (_targetingOverlay is not null
                && world is not null
                && template.UseEffect is not null
                && template.UseEffect.StartsWith("cast_ability:", System.StringComparison.OrdinalIgnoreCase)
                && _content.TryGetAbilityTemplate(template.UseEffect["cast_ability:".Length..], out var ability))
            {
                _targetingOverlay.EnterTargetingForItem(world, playerId, item.InstanceId, template, ability);
                Close();
            }

            return;
        }

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
        builder.AppendLine($"Inventory  {_items.Count}/{inventory?.Capacity ?? 0} stacks  {ResolveTotalCarriedItems()} items");
        builder.AppendLine($"Gold: {ResolveGold()}  Weight: {ResolveTotalWeight():0.0}  Value: {ResolveTotalValue()}");
        builder.AppendLine($"Sort: {CurrentSort}  Auto-equip upgrades: {ResolveAutoEquipLabel()}");
        if (CurrentSort == SortMode.Category)
        {
            builder.AppendLine($"Groups: {ResolveCategorySummary()}");
        }

        builder.AppendLine();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var slotIndex = _firstVisibleIndex + (row * Columns) + column;
                var selected = slotIndex == SelectedIndex;
                var label = slotIndex < _items.Count ? ResolveSlotToken(_items[slotIndex]) : "   ";
                builder.Append(selected ? ">" : " ");
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
            }

            builder.AppendLine();
        }

        builder.Append(ResolveFooterText());
        GridText = builder.ToString().TrimEnd();
        RefreshVisualState();
    }

    private void UpdateDescription()
    {
        if (SelectedIndex >= _items.Count)
        {
            DescriptionText = "Empty slot\n\nCycle sort with Tab to reorganize the bag.";
            DescriptionMarkup = ItemRarityPresentation.EscapeBBCode(DescriptionText);
            HideActiveTooltip();
            RefreshVisualState();
            return;
        }

        var item = _items[SelectedIndex];
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var lines = new List<string>
            {
                template.DisplayName,
                $"Rarity: {ItemRarityPresentation.ResolveDisplayLabel(template.Rarity)}",
                template.Description,
                $"Category: {template.Category}",
                $"Slot: {(template.Slot == EquipSlot.None ? "None" : template.Slot.ToString())}",
                $"Stack: {item.StackCount}/{System.Math.Max(1, template.MaxStack)}",
                $"Unit Value: {template.Value}",
                $"Unit Weight: {template.Weight:0.0}",
                $"Status: {(IsEquipped(item) ? $"Equipped in {ResolveEquippedSlot(item)}" : "Carried")}",
            };

            if (template.MaxCharges > 1)
            {
                var remaining = item.CurrentCharges > 0 ? item.CurrentCharges : template.MaxCharges;
                lines.Add($"Charges: {remaining}/{template.MaxCharges}");
            }

            if (item.StackCount > 1)
            {
                lines.Add($"Stack Value: {template.Value * item.StackCount}");
                lines.Add($"Stack Weight: {template.Weight * item.StackCount:0.0}");
            }

            foreach (var modifier in template.StatModifiers)
            {
                lines.Add($"{modifier.Key}: {modifier.Value:+#;-#;0}");
            }

            if (template.Slot != EquipSlot.None && !IsEquipped(item))
            {
                var comparison = BuildEquipmentComparisonLines(template);
                if (comparison.Count > 0)
                {
                    lines.AddRange(comparison);
                }
            }

            var player = _gameManager?.World?.Player;
            if (player is not null && template.Requirements is not null && template.Requirements.Count > 0)
            {
                var failures = RequirementValidator.GetFailedRequirements(player, template);
                if (failures.Count > 0)
                {
                    foreach (var failure in failures)
                    {
                        lines.Add($"[!] {failure}");
                    }
                }
            }

            lines.Add(ResolvePrimaryActionHint(template, item));
            lines.Add(item.StackCount > 1 ? "D: drop one from stack" : "D: drop item");

            DescriptionText = string.Join("\n", lines);
            DescriptionMarkup = BuildDescriptionMarkup(template, item, lines);
            var tooltipComparison = (template.Slot != EquipSlot.None && !IsEquipped(item))
                ? string.Join("\n", BuildEquipmentComparisonLines(template)) : null;
            ShowActiveTooltip(template, item, tooltipComparison);
            RefreshVisualState();
            return;
        }

        DescriptionText = item.TemplateId;
        DescriptionMarkup = ItemRarityPresentation.EscapeBBCode(DescriptionText);
        HideActiveTooltip();
        RefreshVisualState();
    }

    private void ShowActiveTooltip(ItemTemplate template, ItemInstance item, string? comparisonText)
    {
        _activeTooltip?.Hide();
        _activeTooltip = ResolveTooltip();
        var tooltipPosition = _activeTooltip.ResolveBottomRightPosition(GetViewportRect().Size);
        _activeTooltip.ShowItemTooltip(template, item, tooltipPosition, comparisonText, IsEquipped(item), ResolveEquippedSlot(item));
    }

    private Tooltip ResolveTooltip()
    {
        if (_tooltip is not null)
        {
            return _tooltip;
        }

        if (_activeTooltip is not null)
        {
            return _activeTooltip;
        }

        _activeTooltip = new Tooltip { ZIndex = ZIndex + 5 };
        AddChild(_activeTooltip);
        _activeTooltip._Ready();
        return _activeTooltip;
    }

    private void HideActiveTooltip()
    {
        _activeTooltip?.Hide();
        _activeTooltip = null;
    }

    private Vector2 ResolveSelectedSlotTooltipPosition()
    {
        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var panelPosition = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        var visibleSlotIndex = SelectedIndex - _firstVisibleIndex;
        if (visibleSlotIndex < 0 || visibleSlotIndex >= Columns * Rows)
        {
            return panelPosition + new Vector2(panelSize.X - 320f, PanelPadding + HeaderHeight + 12f);
        }

        var row = visibleSlotIndex / Columns;
        var column = visibleSlotIndex % Columns;
        return panelPosition + new Vector2(
            PanelPadding + (column * (SlotSize + SlotGap)) + SlotSize + 12f,
            PanelPadding + HeaderHeight + 16f + (row * (SlotSize + SlotGap)));
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
        EnsureSelectionVisible();
        UpdateDescription();
        UpdateGrid();
    }

    private void ToggleAutoEquipUpgrades()
    {
        _gameManager?.ToggleAutoEquipUpgrades();
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
        var marker = IsEquipped(item) ? "E" : " ";
        var glyph = ResolveSlotGlyph(item);
        var count = item.StackCount > 1 ? $"x{item.StackCount}" : string.Empty;
        var token = $"{marker}{glyph}{count}";
        return token.PadRight(7);
    }

    private string ResolveSlotGlyph(ItemInstance item)
    {
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return template.Category switch
            {
                ItemCategory.Weapon => "⚔",
                ItemCategory.Armor => "▣",
                ItemCategory.Scroll => "▤",
                ItemCategory.Consumable => IsScroll(template) ? "▤" : "!",
                ItemCategory.Key => "◆",
                ItemCategory.Misc => "◇",
                _ => "?",
            };
        }

        return item.TemplateId[..1].ToUpperInvariant();
    }

    private string ResolveSlotTokenMarkup(ItemInstance item)
    {
        var token = ResolveSlotToken(item);
        if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return ItemRarityPresentation.WrapWithColor(token, template.Rarity);
        }

        return ItemRarityPresentation.EscapeBBCode(token);
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

    private static bool IsScroll(ItemTemplate template)
    {
        return template.TemplateId.Contains("scroll", System.StringComparison.OrdinalIgnoreCase)
            || template.DisplayName.Contains("scroll", System.StringComparison.OrdinalIgnoreCase);
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

    private void EnsureSelectionVisible()
    {
        var pageSize = Columns * Rows;
        if (_items.Count <= pageSize)
        {
            _firstVisibleIndex = 0;
            return;
        }

        _firstVisibleIndex = (SelectedIndex / pageSize) * pageSize;
        var maxFirst = ((_items.Count - 1) / pageSize) * pageSize;
        _firstVisibleIndex = System.Math.Clamp(_firstVisibleIndex, 0, maxFirst);
    }

    public static string BuildEquipmentComparisonText(
        IReadOnlyDictionary<string, int> newModifiers,
        IReadOnlyDictionary<string, int>? equippedModifiers)
    {
        var deltas = new List<string>();
        var allKeys = new HashSet<string>(newModifiers.Keys);
        if (equippedModifiers is not null)
        {
            foreach (var key in equippedModifiers.Keys)
            {
                allKeys.Add(key);
            }
        }

        foreach (var key in allKeys)
        {
            var newVal = newModifiers.TryGetValue(key, out var nv) ? nv : 0;
            var oldVal = equippedModifiers is not null && equippedModifiers.TryGetValue(key, out var ov) ? ov : 0;
            var delta = newVal - oldVal;
            if (delta != 0)
            {
                deltas.Add($"{key}: {delta:+#;-#;0}");
            }
        }

        return deltas.Count > 0
            ? "vs equipped: " + string.Join(", ", deltas)
            : "vs equipped: same stats";
    }

    private string BuildEquipmentComparison(ItemTemplate template)
    {
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        if (inventory is null || _content is null)
        {
            return string.Empty;
        }

        var equipped = inventory.GetEquipped(template.Slot);
        IReadOnlyDictionary<string, int>? equippedModifiers = null;
        if (equipped is not null && _content.TryGetItemTemplate(equipped.Item.TemplateId, out var equippedTemplate))
        {
            equippedModifiers = equippedTemplate.StatModifiers;
        }

        return BuildEquipmentComparisonText(template.StatModifiers, equippedModifiers);
    }

    private IReadOnlyList<string> BuildEquipmentComparisonLines(ItemTemplate template)
    {
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        if (inventory is null || _content is null)
        {
            return System.Array.Empty<string>();
        }

        var equipped = inventory.GetEquipped(template.Slot);
        if (equipped is null || !_content.TryGetItemTemplate(equipped.Item.TemplateId, out var equippedTemplate))
        {
            return new[] { $"Compared with {template.Slot}: no item equipped." };
        }

        var lines = new List<string> { $"Compared with {equippedTemplate.DisplayName}:" };
        var allKeys = new HashSet<string>(template.StatModifiers.Keys);
        foreach (var key in equippedTemplate.StatModifiers.Keys)
        {
            allKeys.Add(key);
        }

        var hasDelta = false;
        foreach (var key in allKeys.OrderBy(key => key, System.StringComparer.Ordinal))
        {
            var newValue = template.StatModifiers.TryGetValue(key, out var next) ? next : 0;
            var oldValue = equippedTemplate.StatModifiers.TryGetValue(key, out var current) ? current : 0;
            var delta = newValue - oldValue;
            if (delta == 0)
            {
                continue;
            }

            hasDelta = true;
            lines.Add($"  {key}: {delta:+#;-#;0}");
        }

        if (!hasDelta)
        {
            lines.Add("  same stats");
        }

        return lines;
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _gridLabel is not null && _descriptionLabel is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        ZIndex = 95;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _panelBackground = new ColorRect { Name = "PanelBackground", Color = UiStyle.PanelBlack(0.96f) };
        _panelBorderTop = new ColorRect { Name = "PanelBorderTop", Color = UiStyle.BorderActive() };
        _panelBorderBottom = new ColorRect { Name = "PanelBorderBottom", Color = UiStyle.BorderActive() };
        _panelBorderLeft = new ColorRect { Name = "PanelBorderLeft", Color = UiStyle.BorderActive() };
        _panelBorderRight = new ColorRect { Name = "PanelBorderRight", Color = UiStyle.BorderActive() };
        _headerBar = new ColorRect { Name = "HeaderBar", Color = UiStyle.PanelHighlight() };
        _headerTitleLabel = new Label { Name = "HeaderTitle", Text = "INVENTORY", Modulate = UiStyle.BrightGold() };
        _headerStatsLabel = new Label { Name = "HeaderStats", Modulate = UiStyle.MutedText() };
        _sortLabel = new Label { Name = "SortLabel", Modulate = UiStyle.FaintText() };
        _footerBar = new ColorRect { Name = "FooterBar", Color = UiStyle.PanelHighlight() };
        _gridLabel = new RichTextLabel
        {
            Name = "GridLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            BbcodeEnabled = true,
            Modulate = UiStyle.Parchment(),
        };
        _descriptionLabel = new RichTextLabel
        {
            Name = "DescriptionLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            BbcodeEnabled = true,
            Modulate = UiStyle.Parchment(),
        };
        _panel.AddChild(_panelBackground);
        _panel.AddChild(_panelBorderTop);
        _panel.AddChild(_panelBorderBottom);
        _panel.AddChild(_panelBorderLeft);
        _panel.AddChild(_panelBorderRight);
        _panel.AddChild(_headerBar);
        _panel.AddChild(_headerTitleLabel);
        _panel.AddChild(_headerStatsLabel);
        _panel.AddChild(_sortLabel);
        _panel.AddChild(_footerBar);
        for (var i = 0; i < Columns * Rows; i++)
        {
            var slot = CreateSlotVisual(i);
            _slotVisuals.Add(slot);
            _panel.AddChild(slot.Background);
            _panel.AddChild(slot.BorderTop);
            _panel.AddChild(slot.BorderBottom);
            _panel.AddChild(slot.BorderLeft);
            _panel.AddChild(slot.BorderRight);
            _panel.AddChild(slot.Glyph);
            _panel.AddChild(slot.StackBadge);
            _panel.AddChild(slot.StackLabel);
            _panel.AddChild(slot.EquippedBadge);
            _panel.AddChild(slot.EquippedLabel);
        }

        CreateFooterHints();
        _panel.AddChild(_gridLabel);
        _panel.AddChild(_descriptionLabel);
        AddChild(_panel);
    }

    private SlotVisual CreateSlotVisual(int index)
    {
        return new SlotVisual
        {
            Background = new InteractiveRect { Name = $"Slot{index}_Background", Index = index, Color = UiStyle.SlotBackground(), InputSubmitted = OnSlotInputSubmitted },
            BorderTop = new ColorRect { Name = $"Slot{index}_BorderTop", Color = UiStyle.BorderSubtle() },
            BorderBottom = new ColorRect { Name = $"Slot{index}_BorderBottom", Color = UiStyle.BorderSubtle() },
            BorderLeft = new ColorRect { Name = $"Slot{index}_BorderLeft", Color = UiStyle.BorderSubtle() },
            BorderRight = new ColorRect { Name = $"Slot{index}_BorderRight", Color = UiStyle.BorderSubtle() },
            Glyph = new Label { Name = $"Slot{index}_Glyph", Modulate = UiStyle.Parchment() },
            StackBadge = new ColorRect { Name = $"Slot{index}_StackBadge", Color = UiStyle.BrightGold() },
            StackLabel = new Label { Name = $"Slot{index}_StackLabel", Modulate = UiStyle.InverseText() },
            EquippedBadge = new ColorRect { Name = $"Slot{index}_EquippedBadge", Color = UiStyle.DeepBlack(0.9f) },
            EquippedLabel = new Label { Name = $"Slot{index}_EquippedLabel", Text = "E", Modulate = UiStyle.ActiveGreen() },
        };
    }

    private void CreateFooterHints()
    {
        if (_footerHintLabels.Count > 0 || _panel is null)
        {
            return;
        }

        var hints = new (string Label, System.Action Action)[]
        {
            ("[E] Equip", SubmitEquip),
            ("[D] Drop", SubmitDrop),
            ("[A] Auto", ToggleAutoEquipUpgrades),
            ("[I/Esc] Close", Close),
        };
        foreach (var hint in hints)
        {
            var label = new FooterHintLabel { Name = $"FooterHint_{_footerHintLabels.Count}", Text = hint.Label, Activated = hint.Action, Modulate = UiStyle.MutedText() };
            _footerHintLabels.Add(label);
            _panel.AddChild(label);
            if (_footerHintLabels.Count < hints.Length)
            {
                var divider = new ColorRect { Name = $"FooterDivider_{_footerDividers.Count}", Color = UiStyle.BorderSubtle() };
                _footerDividers.Add(divider);
                _panel.AddChild(divider);
            }
        }
    }

    private void OnSlotInputSubmitted(int visibleIndex, InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        var absoluteIndex = _firstVisibleIndex + visibleIndex;
        if (absoluteIndex < 0 || absoluteIndex >= System.Math.Max(Columns * Rows, _items.Count))
        {
            return;
        }

        SelectedIndex = absoluteIndex;
        EnsureSelectionVisible();
        UpdateDescription();
        UpdateGrid();

        if (@event is not InputEventMouseButton { Pressed: true } button)
        {
            return;
        }

        if (button.ButtonIndex == MouseButton.Right)
        {
            SubmitPrimary();
        }
        else if (button.ButtonIndex == MouseButton.Middle)
        {
            SubmitDrop();
        }
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _gridLabel is null || _descriptionLabel is null || _panelBackground is null || _headerBar is null || _footerBar is null || _headerTitleLabel is null || _headerStatsLabel is null || _sortLabel is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var contentHeight = Math.Max(0f, panelSize.Y - (PanelPadding * 2f));
        var availableWidth = Math.Max(0f, panelSize.X - (PanelPadding * 2f));
        var gutter = Math.Min(20f, Math.Max(12f, availableWidth * 0.04f));
        var gridWidth = Math.Min(420f, Math.Max(160f, (availableWidth - gutter) * 0.46f));
        var descriptionX = PanelPadding + gridWidth + gutter;
        var descriptionWidth = Math.Max(0f, panelSize.X - descriptionX - PanelPadding);

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        LayoutPanelChrome(panelSize);
        LayoutHeader(panelSize, inventoryCapacity: _gameManager?.World?.Player?.GetComponent<InventoryComponent>()?.Capacity ?? 0);
        LayoutFooter(panelSize);
        LayoutSlots(PanelPadding, PanelPadding + HeaderHeight + 16f);
        _gridLabel.Position = new Vector2(PanelPadding, PanelPadding + HeaderHeight + 16f);
        _gridLabel.Size = new Vector2(gridWidth, System.Math.Max(0f, panelSize.Y - _gridLabel.Position.Y - PanelPadding - FooterHeight - 8f));
        _descriptionLabel.Position = new Vector2(descriptionX, PanelPadding);
        _descriptionLabel.Size = new Vector2(descriptionWidth, Math.Max(0f, contentHeight - FooterHeight - 8f));
        _panel.Visible = Visible;
        _gridLabel.Visible = false;
        _descriptionLabel.Visible = Visible;
        GridMarkup = BuildGridMarkup();
        _gridLabel.Clear();
        _gridLabel.AppendText(GridMarkup);
        _descriptionLabel.Clear();
        _descriptionLabel.AppendText(DescriptionMarkup);
    }

    private void LayoutPanelChrome(Vector2 panelSize)
    {
        if (_panelBackground is null || _panelBorderTop is null || _panelBorderBottom is null || _panelBorderLeft is null || _panelBorderRight is null)
        {
            return;
        }

        _panelBackground.Position = Vector2.Zero;
        _panelBackground.Size = panelSize;
        _panelBorderTop.Position = Vector2.Zero;
        _panelBorderTop.Size = new Vector2(panelSize.X, 1f);
        _panelBorderBottom.Position = new Vector2(0f, panelSize.Y - 1f);
        _panelBorderBottom.Size = new Vector2(panelSize.X, 1f);
        _panelBorderLeft.Position = Vector2.Zero;
        _panelBorderLeft.Size = new Vector2(1f, panelSize.Y);
        _panelBorderRight.Position = new Vector2(panelSize.X - 1f, 0f);
        _panelBorderRight.Size = new Vector2(1f, panelSize.Y);
    }

    private void LayoutHeader(Vector2 panelSize, int inventoryCapacity)
    {
        if (_headerBar is null || _headerTitleLabel is null || _headerStatsLabel is null || _sortLabel is null)
        {
            return;
        }

        _headerBar.Position = new Vector2(PanelPadding, PanelPadding);
        _headerBar.Size = new Vector2(panelSize.X - (PanelPadding * 2f), HeaderHeight);
        _headerTitleLabel.Position = _headerBar.Position + new Vector2(10f, 5f);
        _headerTitleLabel.Size = new Vector2(110f, HeaderHeight);
        _headerStatsLabel.Position = _headerBar.Position + new Vector2(126f, 6f);
        var sortWidth = System.Math.Min(140f, System.Math.Max(80f, _headerBar.Size.X * 0.25f));
        var statsRight = _headerBar.Position.X + _headerBar.Size.X - sortWidth - 12f;
        _headerStatsLabel.Size = new Vector2(System.Math.Max(40f, statsRight - _headerStatsLabel.Position.X), HeaderHeight);
        _headerStatsLabel.Text = $"{_items.Count}/{inventoryCapacity} stacks  ▪  {ResolveTotalCarriedItems()} items  ▪  Wt: {ResolveTotalWeight():0.0}  ▪  Val: {ResolveTotalValue()}  ▪  Gold: {ResolveGold()}";
        _sortLabel.Position = new Vector2(_headerBar.Position.X + _headerBar.Size.X - sortWidth - 8f, _headerBar.Position.Y + 6f);
        _sortLabel.Size = new Vector2(sortWidth, HeaderHeight);
        _sortLabel.Text = $"[Tab] Sort: {CurrentSort}";
    }

    private void LayoutFooter(Vector2 panelSize)
    {
        if (_footerBar is null)
        {
            return;
        }

        _footerBar.Position = new Vector2(PanelPadding, panelSize.Y - PanelPadding - FooterHeight);
        _footerBar.Size = new Vector2(panelSize.X - (PanelPadding * 2f), FooterHeight);
        var x = _footerBar.Position.X + 10f;
        var available = System.Math.Max(48f, (_footerBar.Size.X - 54f) / System.Math.Max(1, _footerHintLabels.Count));
        for (var i = 0; i < _footerHintLabels.Count; i++)
        {
            var label = _footerHintLabels[i];
            label.Position = new Vector2(x, _footerBar.Position.Y + 3f);
            label.Size = new Vector2(available, FooterHeight);
            x += label.Size.X + 4f;
            if (i < _footerDividers.Count)
            {
                var divider = _footerDividers[i];
                divider.Position = new Vector2(x, _footerBar.Position.Y + 4f);
                divider.Size = new Vector2(1f, FooterHeight - 8f);
                x += 6f;
            }
        }
    }

    private void LayoutSlots(float left, float top)
    {
        for (var i = 0; i < _slotVisuals.Count; i++)
        {
            var slotIndex = _firstVisibleIndex + i;
            var row = i / Columns;
            var column = i % Columns;
            var position = new Vector2(left + (column * (SlotSize + SlotGap)), top + (row * (SlotSize + SlotGap)));
            var item = slotIndex < _items.Count ? _items[slotIndex] : null;
            var selected = slotIndex == SelectedIndex;
            UpdateSlotVisual(_slotVisuals[i], position, item, selected);
        }
    }

    private void UpdateSlotVisual(SlotVisual slot, Vector2 position, ItemInstance? item, bool selected)
    {
        var occupied = item is not null;
        var equipped = item is not null && IsEquipped(item);
        var borderColor = selected ? UiStyle.BrightGold() : equipped ? UiStyle.ActiveGreen() : UiStyle.BorderSubtle();
        var borderSize = selected || equipped ? 2f : 1f;
        slot.Background.Position = position;
        slot.Background.Size = new Vector2(SlotSize, SlotSize);
        slot.Background.Color = selected ? UiStyle.SlotSelected() : UiStyle.SlotBackground();
        slot.BorderTop.Position = position;
        slot.BorderTop.Size = new Vector2(SlotSize, borderSize);
        slot.BorderBottom.Position = new Vector2(position.X, position.Y + SlotSize - borderSize);
        slot.BorderBottom.Size = new Vector2(SlotSize, borderSize);
        slot.BorderLeft.Position = position;
        slot.BorderLeft.Size = new Vector2(borderSize, SlotSize);
        slot.BorderRight.Position = new Vector2(position.X + SlotSize - borderSize, position.Y);
        slot.BorderRight.Size = new Vector2(borderSize, SlotSize);
        slot.BorderTop.Color = slot.BorderBottom.Color = slot.BorderLeft.Color = slot.BorderRight.Color = borderColor;
        slot.Glyph.Position = position + new Vector2(18f, 13f);
        slot.Glyph.Size = new Vector2(24f, 24f);
        slot.Glyph.Text = occupied ? ResolveSlotGlyph(item!) : string.Empty;
        slot.Glyph.Modulate = occupied && _content is not null && _content.TryGetItemTemplate(item!.TemplateId, out var template)
            ? ItemRarityPresentation.ResolveColor(template.Rarity)
            : UiStyle.Parchment();
        slot.StackBadge.Visible = occupied && item!.StackCount > 1;
        slot.StackBadge.Position = position + new Vector2(SlotSize - 18f, SlotSize - 16f);
        slot.StackBadge.Size = new Vector2(16f, 14f);
        slot.StackLabel.Visible = slot.StackBadge.Visible;
        slot.StackLabel.Position = slot.StackBadge.Position + new Vector2(2f, 0f);
        slot.StackLabel.Size = slot.StackBadge.Size;
        slot.StackLabel.Text = occupied ? item!.StackCount.ToString() : string.Empty;
        slot.EquippedBadge.Visible = equipped;
        slot.EquippedBadge.Position = position + new Vector2(2f, SlotSize - 15f);
        slot.EquippedBadge.Size = new Vector2(13f, 13f);
        slot.EquippedLabel.Visible = equipped;
        slot.EquippedLabel.Position = slot.EquippedBadge.Position + new Vector2(3f, -1f);
        slot.EquippedLabel.Size = slot.EquippedBadge.Size;
    }

    private Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }

    private string BuildGridMarkup()
    {
        var builder = new StringBuilder();
        var inventory = _gameManager?.World?.Player?.GetComponent<InventoryComponent>();
        builder.AppendLine($"[color={UiStyle.LegendaryHex}]Inventory[/color]  {ItemRarityPresentation.EscapeBBCode($"{_items.Count}/{inventory?.Capacity ?? 0} stacks  {ResolveTotalCarriedItems()} items")}");
        builder.AppendLine($"[color={UiStyle.CommonHex}]{ItemRarityPresentation.EscapeBBCode($"Gold: {ResolveGold()}  Weight: {ResolveTotalWeight():0.0}  Value: {ResolveTotalValue()}")}[/color]");
        builder.AppendLine($"[color={UiStyle.CommonHex}]{ItemRarityPresentation.EscapeBBCode($"Sort: {CurrentSort}  Auto-equip upgrades: {ResolveAutoEquipLabel()}")}[/color]");
        if (CurrentSort == SortMode.Category)
        {
            builder.AppendLine(ItemRarityPresentation.EscapeBBCode($"Groups: {ResolveCategorySummary()}"));
        }

        builder.AppendLine();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var slotIndex = _firstVisibleIndex + (row * Columns) + column;
                var selected = slotIndex == SelectedIndex;
                var label = slotIndex < _items.Count ? ResolveSlotTokenMarkup(_items[slotIndex]) : "       ";
                builder.Append(selected ? $"[color={UiStyle.LegendaryHex}]>[/color]" : " ");
                builder.Append(selected ? $"[color={UiStyle.LegendaryHex}][lb]" : "[lb]");
                builder.Append(label);
                builder.Append(selected ? $"[rb][/color] " : "[rb] ");
            }

            builder.AppendLine();
        }

        builder.Append(ItemRarityPresentation.EscapeBBCode(ResolveFooterText()));
        return builder.ToString().TrimEnd();
    }

    private string ResolveAutoEquipLabel() => _gameManager?.AutoEquipUpgradesEnabled == true ? "On" : "Off";

    private string ResolveFooterText()
    {
        if (TryGetSelectedItem(out var item)
            && _content is not null
            && _content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var primary = template.Slot == EquipSlot.None
                ? (CanUseFromInventory(template) ? "Enter/U use" : "Aimed item")
                : (IsEquipped(item) ? "Enter/E unequip" : "Enter/E equip");
            var drop = item.StackCount > 1 ? "D drop one" : "D drop";
            return $"{ResolvePageText()}  {primary}  {drop}  A auto-equip  Tab sort  I/Esc close";
        }

        return $"{ResolvePageText()}  Arrow keys move  Tab sort  A auto-equip  I/Esc close";
    }

    private string ResolvePageText()
    {
        var pageSize = Columns * Rows;
        var totalPages = System.Math.Max(1, (_items.Count + pageSize - 1) / pageSize);
        var currentPage = System.Math.Min(totalPages, (_firstVisibleIndex / pageSize) + 1);
        return $"Page {currentPage}/{totalPages}";
    }

    private string ResolveCategorySummary()
    {
        return string.Join("  ", _items
            .Select(item => _content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template)
                ? $"{ResolveSlotGlyph(item)} {template.Category}"
                : "? Unknown")
            .Distinct());
    }

    private string ResolvePrimaryActionHint(ItemTemplate template, ItemInstance item)
    {
        if (template.Slot != EquipSlot.None)
        {
            return IsEquipped(item) ? "E/Enter: unequip" : "E/Enter: equip";
        }

        return CanUseFromInventory(template)
            ? "U/Enter: use item"
            : "Requires targeting: aim this item from the field before use.";
    }

    private bool CanUseFromInventory(ItemTemplate template)
    {
        if (template.Slot != EquipSlot.None)
        {
            return false;
        }

        return !template.RequiresTargetSelection;
    }

    private static string BuildDescriptionMarkup(ItemTemplate template, ItemInstance item, IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine(ItemRarityPresentation.WrapWithColor(template.DisplayName, template.Rarity));
        builder.AppendLine(ItemRarityPresentation.WrapWithColor($"Rarity: {ItemRarityPresentation.ResolveDisplayLabel(template.Rarity)}", template.Rarity));
        for (var i = 2; i < lines.Count; i++)
        {
            builder.AppendLine(ItemRarityPresentation.EscapeBBCode(lines[i]));
        }

        return builder.ToString().TrimEnd();
    }

    private int ResolveGold()
    {
        return _gameManager?.World?.Player?.GetComponent<WalletComponent>()?.Gold ?? 0;
    }

    private int ResolveTotalCarriedItems()
    {
        var total = 0;
        foreach (var item in _items)
        {
            total += System.Math.Max(1, item.StackCount);
        }

        return total;
    }

    private double ResolveTotalWeight()
    {
        var total = 0.0;
        foreach (var item in _items)
        {
            if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
            {
                total += template.Weight * System.Math.Max(1, item.StackCount);
            }
        }

        return total;
    }

    private int ResolveTotalValue()
    {
        var total = 0;
        foreach (var item in _items)
        {
            if (_content is not null && _content.TryGetItemTemplate(item.TemplateId, out var template))
            {
                total += template.Value * System.Math.Max(1, item.StackCount);
            }
        }

        return total;
    }
}
