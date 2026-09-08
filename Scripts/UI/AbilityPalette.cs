using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class AbilityPalette : MenuBase
{
    private sealed record AbilityEntry(AbilityTemplate Template, int Cooldown);

    private readonly List<AbilityEntry> _entries = new();
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private TargetingOverlay? _targetingOverlay;
    private EntityId _playerId;

    public AbilityPalette()
    {
        Name = "AbilityPalette";
        Title = "ABILITIES";
        Visible = false;
    }

    public IReadOnlyList<AbilityTemplate> ListedAbilities => _entries.Select(entry => entry.Template).ToArray();

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content, TargetingOverlay targetingOverlay)
    {
        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
        _targetingOverlay = targetingOverlay;
    }

    public bool OpenForPlayer()
    {
        var player = _gameManager?.World?.Player;
        if (player is null || _content is null)
        {
            return false;
        }

        _playerId = player.Id;
        _entries.Clear();
        var cooldowns = player.GetComponent<CooldownComponent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in player.GetComponent<AbilitiesComponent>()?.Slots ?? Enumerable.Empty<EnemyAbilitySlot>())
        {
            if (!seen.Add(slot.AbilityId) || !_content.TryGetAbilityTemplate(slot.AbilityId, out var ability))
            {
                continue;
            }

            _entries.Add(new AbilityEntry(ability, cooldowns?.GetCooldown(ability.AbilityId) ?? 0));
        }

        if (_entries.Count == 0)
        {
            _eventBus?.EmitLogMessage("No abilities are available.", LogCategory.Warning);
            return false;
        }

        ConfigureOptions(_entries.Select((entry, index) => FormatOption(index, entry)).ToArray());
        Open();
        return true;
    }

    protected override string BuildBodyText()
    {
        return "Choose an owned technique. Self techniques cast immediately; other shapes enter targeting.";
    }

    protected override string BuildFooterText() => "[1-4] cast  [Enter] select  [B/Esc] close";

    protected override bool HandleCustomKey(Key key)
    {
        if (key == Key.B)
        {
            Close();
            return true;
        }

        var index = key switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            Key.Key4 => 3,
            _ => -1,
        };
        if (index < 0 || index >= _entries.Count)
        {
            return false;
        }

        SetSelection(index);
        ActivateSelected();
        return true;
    }

    protected override void ActivateSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _entries.Count)
        {
            return;
        }

        var entry = _entries[SelectedIndex];
        if (entry.Cooldown > 0)
        {
            _eventBus?.EmitLogMessage($"{entry.Template.DisplayName} is ready in {entry.Cooldown} turn(s).", LogCategory.Warning);
            return;
        }

        var world = _gameManager?.World;
        var actor = world?.GetEntity(_playerId);
        if (world is null || actor is null || _targetingOverlay is null)
        {
            Close();
            return;
        }

        if (string.Equals(entry.Template.Targeting.Type, "self", StringComparison.OrdinalIgnoreCase))
        {
            _eventBus?.EmitPlayerActionSubmitted(new CastAbilityAction(actor.Id, entry.Template, actor.Position));
            Close();
            return;
        }

        Close();
        _targetingOverlay.EnterTargetingForAbility(world, actor.Id, entry.Template);
    }

    private static string FormatOption(int index, AbilityEntry entry)
    {
        var targeting = entry.Template.Targeting.Type.Replace('_', ' ');
        var range = entry.Template.Targeting.Type == "self" ? "self" : $"range {entry.Template.Targeting.Range}";
        var cooldown = entry.Cooldown > 0 ? $" [CD {entry.Cooldown}]" : string.Empty;
        return $"{index + 1}. {entry.Template.DisplayName} - {targeting}, {range}, {entry.Template.EnergyCost} energy{cooldown}";
    }
}
