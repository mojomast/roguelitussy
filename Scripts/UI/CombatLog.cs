using Godot;
using System.Collections.Generic;

namespace Roguelike.Godot;

public partial class CombatLog : CanvasLayer
{
    private const int MaxMessages = 100;

    private RichTextLabel _log = null!;
    private readonly List<string> _messages = new();

    public override void _Ready()
    {
        _log = GetNode<RichTextLabel>("%LogText");
        _log.BbcodeEnabled = true;
        _log.ScrollFollowing = true;

        var bus = EventBus.Instance;
        bus.EntityAttacked += OnEntityAttacked;
        bus.EntityDied += OnEntityDied;
        bus.ItemUsed += OnItemUsed;
        bus.StatusEffectApplied += OnStatusEffectApplied;
        bus.StatusEffectRemoved += OnStatusEffectRemoved;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.EntityAttacked -= OnEntityAttacked;
        bus.EntityDied -= OnEntityDied;
        bus.ItemUsed -= OnItemUsed;
        bus.StatusEffectApplied -= OnStatusEffectApplied;
        bus.StatusEffectRemoved -= OnStatusEffectRemoved;
    }

    private void OnEntityAttacked(string attackerId, string defenderId, int damage, bool isCritical, bool isMiss)
    {
        if (isMiss)
        {
            AddMessage($"[color=gray]{attackerId} missed {defenderId}.[/color]");
        }
        else if (isCritical)
        {
            AddMessage($"[color=yellow]{attackerId} critically hit {defenderId} for {damage} damage![/color]");
        }
        else
        {
            AddMessage($"{attackerId} hit {defenderId} for {damage} damage.");
        }
    }

    private void OnEntityDied(string entityId, string killerEntityId)
    {
        AddMessage($"[color=red]{entityId} was slain by {killerEntityId}.[/color]");
    }

    private void OnItemUsed(string entityId, string itemTemplateId, string effectDescription)
    {
        AddMessage($"[color=cyan]{entityId} used {itemTemplateId}: {effectDescription}[/color]");
    }

    private void OnStatusEffectApplied(string entityId, int effectType, int duration)
    {
        AddMessage($"[color=orange]{entityId} gained status effect {effectType} for {duration} turns.[/color]");
    }

    private void OnStatusEffectRemoved(string entityId, int effectType)
    {
        AddMessage($"[color=gray]{entityId} lost status effect {effectType}.[/color]");
    }

    public void AddMessage(string message)
    {
        _messages.Add(message);
        while (_messages.Count > MaxMessages)
        {
            _messages.RemoveAt(0);
        }
        RebuildLog();
    }

    private void RebuildLog()
    {
        _log.Clear();
        foreach (var msg in _messages)
        {
            _log.AppendText(msg + "\n");
        }
    }
}
