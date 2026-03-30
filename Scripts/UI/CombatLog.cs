using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class CombatLog : Control
{
    private const int MaxMessages = 100;
    private readonly Queue<string> _messages = new();
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public string RenderedText { get; private set; } = string.Empty;

    public CombatLog()
    {
        Name = "CombatLog";
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.LogMessage -= OnLogMessage;
            _eventBus.DamageDealt -= OnDamageDealt;
            _eventBus.EntityDied -= OnEntityDied;
            _eventBus.ItemPickedUp -= OnItemPickedUp;
            _eventBus.StatusEffectApplied -= OnStatusEffectApplied;
            _eventBus.SaveCompleted -= OnSaveCompleted;
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is null)
        {
            return;
        }

        _eventBus.LogMessage += OnLogMessage;
        _eventBus.DamageDealt += OnDamageDealt;
        _eventBus.EntityDied += OnEntityDied;
        _eventBus.ItemPickedUp += OnItemPickedUp;
        _eventBus.StatusEffectApplied += OnStatusEffectApplied;
        _eventBus.SaveCompleted += OnSaveCompleted;
        _eventBus.LoadCompleted += OnLoadCompleted;
        _eventBus.FloorChanged += OnFloorChanged;
    }

    public IReadOnlyCollection<string> Messages => _messages;

    private void OnLogMessage(string message)
    {
        AddMessage("white", message);
    }

    private void OnDamageDealt(DamageResult damage)
    {
        var attacker = ResolveName(damage.AttackerId);
        var defender = ResolveName(damage.DefenderId);
        var message = damage.IsMiss
            ? $"{attacker} misses {defender}."
            : $"{attacker} hits {defender} for {damage.FinalDamage} damage.";
        AddMessage(damage.DefenderId == _gameManager?.World?.Player.Id ? "red" : "orange", message);
    }

    private void OnEntityDied(EntityId entityId)
    {
        AddMessage("orange", $"{ResolveName(entityId)} dies.");
    }

    private void OnItemPickedUp(EntityId entityId, ItemInstance item)
    {
        AddMessage("cyan", $"{ResolveName(entityId)} picks up {ResolveItemName(item.TemplateId)}.");
    }

    private void OnStatusEffectApplied(EntityId entityId, StatusEffectInstance effect)
    {
        AddMessage("yellow", $"{ResolveName(entityId)} gains {effect.Type} ({effect.RemainingTurns}).");
    }

    private void OnSaveCompleted(bool success)
    {
        AddMessage("gray", success ? "Save completed." : "Save failed.");
    }

    private void OnLoadCompleted(bool success)
    {
        AddMessage("gray", success ? "Load completed." : "Load failed.");
    }

    private void OnFloorChanged(int floor)
    {
        AddMessage("gray", $"Reached floor {floor}.");
    }

    private void AddMessage(string color, string message)
    {
        _messages.Enqueue($"[color={color}]{EscapeBBCode(message)}[/color]");
        while (_messages.Count > MaxMessages)
        {
            _messages.Dequeue();
        }

        var builder = new StringBuilder();
        foreach (var line in _messages)
        {
            builder.AppendLine(line);
        }

        RenderedText = builder.ToString().TrimEnd();
    }

    private string ResolveName(EntityId entityId)
    {
        return _gameManager?.World?.GetEntity(entityId)?.Name ?? entityId.ToString();
    }

    private string ResolveItemName(string templateId)
    {
        var content = _gameManager?.Content;
        return content is not null && content.TryGetItemTemplate(templateId, out var template)
            ? template.DisplayName
            : templateId;
    }

    private static string EscapeBBCode(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                '[' => "[lb]",
                ']' => "[rb]",
                _ => character,
            });
        }

        return builder.ToString();
    }
}