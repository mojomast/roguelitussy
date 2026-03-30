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

    public string SummaryText { get; private set; } = string.Empty;

    public CharacterSheet()
    {
        Name = "CharacterSheet";
        Visible = false;
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
        if (_gameManager?.World?.Player.Id == entityId)
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
            return;
        }

        var player = world.Player;
        var inventory = player.GetComponent<InventoryComponent>();
        var builder = new StringBuilder();
        builder.AppendLine("Character Sheet");
        builder.AppendLine($"Name: {player.Name}");
        builder.AppendLine($"Floor: {world.Depth}");
        builder.AppendLine($"Turn: {world.TurnNumber}");
        builder.AppendLine();
        builder.AppendLine($"HP: {player.Stats.HP} / {player.Stats.MaxHP}");
        builder.AppendLine($"Attack: {player.Stats.Attack}{FormatEquipmentBonus(inventory, EquipSlot.MainHand)}");
        builder.AppendLine($"Defense: {player.Stats.Defense}{FormatEquipmentBonus(inventory, EquipSlot.Body)}");
        builder.AppendLine($"Speed: {player.Stats.Speed}");
        builder.AppendLine();
        builder.AppendLine($"Weapon: {ResolveEquippedItemName(inventory, EquipSlot.MainHand)}");
        builder.AppendLine($"Armor: {ResolveEquippedItemName(inventory, EquipSlot.Body)}");
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

        SummaryText = builder.ToString().TrimEnd();
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
}