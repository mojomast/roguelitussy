using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class QuickSlotHotbar : Control
{
    private const int SlotCount = UIActionFactory.QuickUseSlotCount;
    private readonly List<Panel> _slotPanels = new();
    private readonly List<ColorRect> _slotBackgrounds = new();
    private readonly List<Label> _slotLabels = new();
    private readonly string[] _slotTexts = new string[SlotCount];
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private bool _suppressed;

    public QuickSlotHotbar()
    {
        Name = "QuickSlotHotbar";
        ZIndex = 10;
        for (var i = 0; i < SlotCount; i++)
        {
            _slotTexts[i] = EmptySlotText(i);
        }

        EnsureVisualTree();
        ApplyResponsiveLayout();
        Refresh();
    }

    public IReadOnlyList<string> SlotTexts => _slotTexts;

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content ?? gameManager?.Content;

        if (_eventBus is not null)
        {
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        Refresh();
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        RefreshVisibility();
    }

    public void RefreshVisibility(bool gameplayVisible)
    {
        SetSuppressed(!gameplayVisible);
    }

    public void Refresh()
    {
        EnsureVisualTree();
        ApplyResponsiveLayout();

        for (var i = 0; i < SlotCount; i++)
        {
            _slotTexts[i] = EmptySlotText(i);
        }

        var world = _gameManager?.World;
        var player = world?.Player;
        if (world is not null && player is not null && _content is not null)
        {
            var items = UIActionFactory.GetQuickUseItems(world, _content, player.Id, SlotCount);
            for (var i = 0; i < items.Count && i < SlotCount; i++)
            {
                _slotTexts[i] = BuildSlotText(i, items[i]);
            }
        }

        for (var i = 0; i < _slotLabels.Count; i++)
        {
            _slotLabels[i].Text = CompactSlotText(_slotTexts[i]);
            _slotLabels[i].Modulate = _slotTexts[i].Contains("empty", System.StringComparison.OrdinalIgnoreCase)
                ? UiStyle.MutedText()
                : UiStyle.Parchment();
        }

        RefreshVisibility();
    }

    private void OnInventoryChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            Refresh();
        }
    }

    private void OnTurnCompleted()
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

    private void EnsureVisualTree()
    {
        if (_slotPanels.Count > 0)
        {
            return;
        }

        for (var i = 0; i < SlotCount; i++)
        {
            var panel = new Panel
            {
                Name = $"QuickSlot_{i + 1}",
                Position = new Vector2(i * 108f, 0f),
                Size = new Vector2(102f, 48f),
                Modulate = UiStyle.GoldTrim(),
            };

            var background = new ColorRect
            {
                Name = $"QuickSlotBackground_{i + 1}",
                Position = Vector2.Zero,
                Size = panel.Size,
                Color = UiStyle.SlotBackground(0.92f),
            };

            var label = new Label
            {
                Name = $"QuickSlotLabel_{i + 1}",
                Position = new Vector2(6f, 8f),
                Size = new Vector2(90f, 32f),
                Text = _slotTexts[i],
                Modulate = UiStyle.MutedText(),
            };

            panel.AddChild(background);
            panel.AddChild(label);
            AddChild(panel);
            _slotPanels.Add(panel);
            _slotBackgrounds.Add(background);
            _slotLabels.Add(label);
        }
    }

    private void ApplyResponsiveLayout()
    {
        var viewportSize = ResolveViewportSize();
        var width = System.Math.Min(640f, System.Math.Max(300f, viewportSize.X - 32f));
        var height = 54f;
        var slotGap = width < 420f ? 4f : 8f;
        var slotWidth = System.Math.Max(48f, (width - (slotGap * (SlotCount - 1))) / SlotCount);
        Size = new Vector2(width, height);
        Position = new Vector2(
            System.Math.Max(8f, (viewportSize.X - width) * 0.5f),
            System.Math.Max(8f, viewportSize.Y - height - 12f));

        for (var i = 0; i < _slotPanels.Count; i++)
        {
            var x = i * (slotWidth + slotGap);
            _slotPanels[i].Position = new Vector2(x, 0f);
            _slotPanels[i].Size = new Vector2(slotWidth, 48f);
            _slotBackgrounds[i].Position = Vector2.Zero;
            _slotBackgrounds[i].Size = _slotPanels[i].Size;
            _slotLabels[i].Position = new Vector2(6f, 8f);
            _slotLabels[i].Size = new Vector2(System.Math.Max(0f, slotWidth - 12f), 32f);
        }
    }

    private void RefreshVisibility()
    {
        Visible = !_suppressed && _gameManager?.CurrentState == GameManager.GameState.Playing;
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }

    private static string CompactSlotText(string text)
    {
        const int MaxLength = 18;
        if (text.Length <= MaxLength)
        {
            return text;
        }

        return text[..(MaxLength - 3)].TrimEnd() + "...";
    }

    private string BuildSlotText(int slotIndex, ItemInstance item)
    {
        var displayName = _content?.TryGetItemTemplate(item.TemplateId, out var template) == true
            ? template.DisplayName
            : item.TemplateId;
        if (item.StackCount > 1)
        {
            displayName += $" x{item.StackCount}";
        }

        return $"{slotIndex + 1}: {displayName}";
    }

    private static string EmptySlotText(int slotIndex) => $"{slotIndex + 1}: empty";
}
