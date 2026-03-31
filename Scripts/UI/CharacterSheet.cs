using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class CharacterSheet : Control
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IContentDatabase? _content;
    private Panel? _panel;
    private Label? _label;

    private const float PanelWidth = 520f;
    private const float PanelHeight = 520f;
    private const float PanelPadding = 18f;
    private const float OuterMargin = 24f;

    public string SummaryText { get; private set; } = string.Empty;

    public CharacterSheet()
    {
        Name = "CharacterSheet";
        Visible = false;
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        Refresh();
    }

    public void Open()
    {
        Visible = true;
        Refresh();
        RefreshVisualState();
    }

    public void Close()
    {
        Visible = false;
        RefreshVisualState();
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        if (key is Key.C or Key.Escape)
        {
            Close();
            return true;
        }

        return false;
    }

    private void OnTurnCompleted()
    {
        Refresh();
    }

    private void OnInventoryChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
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
            SummaryText = string.Empty;
            RefreshVisualState();
            return;
        }

        var player = world.Player;
        if (player is null)
        {
            SummaryText = "Character Sheet\nNo active run";
            RefreshVisualState();
            return;
        }

        var inventory = player.GetComponent<InventoryComponent>();
        var character = _gameManager?.CharacterOptions;
        var builder = new StringBuilder();
        builder.AppendLine("Character Sheet");
        builder.AppendLine($"Name: {player.Name}");
        if (character is not null)
        {
            builder.AppendLine($"Archetype: {character.Archetype}");
            builder.AppendLine($"Origin: {character.Origin}");
            builder.AppendLine($"Trait: {character.Trait}");
        }
        builder.AppendLine($"Floor: {world.Depth}");
        builder.AppendLine($"Turn: {world.TurnNumber}");
        builder.AppendLine();
        builder.AppendLine($"HP: {player.Stats.HP} / {player.Stats.MaxHP}");
        builder.AppendLine($"Attack: {player.Stats.Attack}{FormatEquipmentBonus(inventory, EquipSlot.MainHand)}");
        builder.AppendLine($"Defense: {player.Stats.Defense}{FormatEquipmentBonus(inventory, EquipSlot.Body)}");
        builder.AppendLine($"Accuracy: {player.Stats.Accuracy}");
        builder.AppendLine($"Evasion: {player.Stats.Evasion}");
        builder.AppendLine($"Speed: {player.Stats.Speed}");
        builder.AppendLine($"View Radius: {player.Stats.ViewRadius}");
        builder.AppendLine($"Inventory: {inventory?.Items.Count ?? 0}/{inventory?.Capacity ?? 0}");
        builder.AppendLine();
        builder.AppendLine($"Weapon: {ResolveEquippedItemName(inventory, EquipSlot.MainHand)}");
        builder.AppendLine($"Off-hand: {ResolveEquippedItemName(inventory, EquipSlot.OffHand)}");
        builder.AppendLine($"Head: {ResolveEquippedItemName(inventory, EquipSlot.Head)}");
        builder.AppendLine($"Armor: {ResolveEquippedItemName(inventory, EquipSlot.Body)}");
        builder.AppendLine($"Feet: {ResolveEquippedItemName(inventory, EquipSlot.Feet)}");
        builder.AppendLine($"Ring: {ResolveEquippedItemName(inventory, EquipSlot.Ring)}");
        builder.AppendLine($"Amulet: {ResolveEquippedItemName(inventory, EquipSlot.Amulet)}");
        builder.AppendLine();
        builder.AppendLine("Status Effects:");

        var effects = StatusEffectProcessor.GetEffects(player);
        if (effects.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var effect in effects)
            {
                builder.AppendLine($"- {effect.Type} ({effect.RemainingTurns} turns, x{effect.Magnitude})");
            }
        }

        builder.AppendLine();
        builder.Append("Close: C/Esc");
        SummaryText = builder.ToString().TrimEnd();
        RefreshVisualState();
    }

    private string ResolveEquippedItemName(InventoryComponent? inventory, EquipSlot slot)
    {
        var equipped = inventory?.GetEquipped(slot);
        if (equipped is null)
        {
            return "None";
        }

        if (_content is not null && _content.TryGetItemTemplate(equipped.Item.TemplateId, out var template))
        {
            return template.DisplayName;
        }

        return equipped.Item.TemplateId;
    }

    private static string FormatEquipmentBonus(InventoryComponent? inventory, EquipSlot slot)
    {
        var equipped = inventory?.GetEquipped(slot);
        if (equipped is null || equipped.StatModifiers.Count == 0)
        {
            return string.Empty;
        }

        var stat = slot == EquipSlot.MainHand ? "attack" : "defense";
        return equipped.StatModifiers.TryGetValue(stat, out var bonus) ? $" ({bonus:+#;-#;0})" : string.Empty;
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _label is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        ZIndex = 90;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _label = new Label
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(
                Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
                Math.Max(0f, panelSize.Y - (PanelPadding * 2f))),
        };
        _panel.AddChild(_label);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _label is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);
        _label.Position = new Vector2(PanelPadding, PanelPadding);
        _label.Size = new Vector2(
            Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Text = SummaryText;
    }

    private Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}