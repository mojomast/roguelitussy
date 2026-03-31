using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class CombatLog : Control
{
    private const int MaxMessages = 100;
    private const float PanelWidth = 500f;
    private const float PanelHeight = 220f;
    private const float OuterMargin = 20f;
    private const float PanelPadding = 12f;
    private readonly Queue<string> _messages = new();
    private Panel? _panel;
    private RichTextLabel? _textLabel;
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public string RenderedText { get; private set; } = string.Empty;

    public CombatLog()
    {
        Name = "CombatLog";
        Visible = true;
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void RefreshConsole()
    {
        RefreshVisualState();
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
        RefreshVisualState();
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
        var content = _gameManager?.Content;
        if (content is not null && content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var actorName = ItemRarityPresentation.EscapeBBCode(ResolveName(entityId));
            var itemName = ItemRarityPresentation.WrapWithColor(template.DisplayName, template.Rarity);
            if (ItemRarityPresentation.IsHighlighted(template.Rarity))
            {
                var callout = ItemRarityPresentation.EscapeBBCode(ItemRarityPresentation.ResolvePickupCallout(template));
                AddMarkupMessage($"[color=cyan]{actorName} secures {callout}: [/color]{itemName}[color=cyan].[/color]");
                return;
            }

            AddMarkupMessage($"[color=cyan]{actorName} picks up [/color]{itemName}[color=cyan].[/color]");
            return;
        }

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
        AddMarkupMessage($"[color={color}]{ItemRarityPresentation.EscapeBBCode(message)}[/color]");
    }

    private void AddMarkupMessage(string markup)
    {
        _messages.Enqueue(markup);
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
        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _textLabel is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        ZIndex = 40;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _textLabel = new RichTextLabel
        {
            Name = "TextConsole",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(
                System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
                System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f))),
            BbcodeEnabled = true,
            ScrollFollowing = true,
        };
        _panel.AddChild(_textLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _textLabel is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var gameplayVisible = _gameManager?.CurrentState != GameManager.GameState.MainMenu;

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = new Vector2(OuterMargin, viewportSize.Y - panelSize.Y - OuterMargin);
        _textLabel.Position = new Vector2(PanelPadding, PanelPadding);
        _textLabel.Size = new Vector2(
            System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _panel.Visible = gameplayVisible;
        _textLabel.Visible = gameplayVisible;
        _textLabel.Clear();
        if (!string.IsNullOrWhiteSpace(RenderedText))
        {
            _textLabel.AppendText(RenderedText);
        }
    }

    private static Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1600f, 900f);
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
}