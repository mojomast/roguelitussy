using System.Collections.Generic;
using System.Linq;
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
    private ColorRect? _background;
    private ColorRect? _fade;
    private RichTextLabel? _textLabel;
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private bool _suppressedByOverlay;

    public string RenderedText { get; private set; } = string.Empty;

    public bool IsSuppressed => _suppressedByOverlay;

    public bool ConsoleVisible => _panel?.Visible ?? false;

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

    public void SetSuppressed(bool suppressed)
    {
        if (_suppressedByOverlay == suppressed)
        {
            return;
        }

        _suppressedByOverlay = suppressed;
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
        AddMessage(message);
    }

    private void OnDamageDealt(DamageResult damage)
    {
        var attacker = ResolveName(damage.AttackerId);
        var defender = ResolveName(damage.DefenderId);
        var message = damage.IsMiss
            ? $"{attacker} misses {defender}."
            : $"{attacker} hits {defender} for {damage.FinalDamage} damage.";
        AddMessage(message);
    }

    private void OnEntityDied(EntityId entityId)
    {
        AddMessage($"{ResolveName(entityId)} dies.");
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

        AddMessage($"{ResolveName(entityId)} picks up {ResolveItemName(item.TemplateId)}.");
    }

    private void OnStatusEffectApplied(EntityId entityId, StatusEffectInstance effect)
    {
        AddMessage($"{ResolveName(entityId)} gains {effect.Type} ({effect.RemainingTurns}).");
    }

    private void OnSaveCompleted(bool success)
    {
        AddMessage(success ? "Save completed." : "Save failed.");
    }

    private void OnLoadCompleted(bool success)
    {
        AddMessage(success ? "Load completed." : "Load failed.");
    }

    private void OnFloorChanged(int floor)
    {
        AddMessage($"Reached floor {floor}.");
    }

    private void AddMessage(string message)
    {
        AddMarkupMessage($"[color={UiStyle.ToHex(GetEntryColor(message))}]{ItemRarityPresentation.EscapeBBCode(message)}[/color]");
    }

    private Color GetEntryColor(string message)
    {
        var playerName = _gameManager?.World?.Player?.Name ?? "Rook";
        if (message.Contains("dies", System.StringComparison.OrdinalIgnoreCase)
            || message.Contains("defeated", System.StringComparison.OrdinalIgnoreCase))
        {
            return UiStyle.ActiveGreen();
        }

        if (message.Contains("moves to", System.StringComparison.OrdinalIgnoreCase)
            || message.Contains("Waiting", System.StringComparison.OrdinalIgnoreCase))
        {
            return UiStyle.FaintText();
        }

        if (message.Contains("casts", System.StringComparison.OrdinalIgnoreCase)
            || message.Contains("applies", System.StringComparison.OrdinalIgnoreCase)
            || message.Contains("gains", System.StringComparison.OrdinalIgnoreCase))
        {
            return UiStyle.WarningOrange();
        }

        if (message.Contains($"hits {playerName}", System.StringComparison.OrdinalIgnoreCase)
            || message.Contains("hits Rook", System.StringComparison.OrdinalIgnoreCase))
        {
            return UiStyle.DangerRed();
        }

        if (message.Contains("hits", System.StringComparison.OrdinalIgnoreCase))
        {
            return UiStyle.BrightGold();
        }

        return UiStyle.Parchment();
    }

    private void AddMarkupMessage(string markup)
    {
        _messages.Enqueue(markup);
        while (_messages.Count > MaxMessages)
        {
            _messages.Dequeue();
        }

        var builder = new StringBuilder();
        foreach (var line in _messages.TakeLast(6))
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
        _background = new ColorRect { Name = "Background", Color = UiStyle.PanelBlack(0.78f) };
        _fade = new ColorRect { Name = "TopFade", Color = UiStyle.PanelBlack(0.55f) };
        _textLabel = new RichTextLabel
        {
            Name = "TextConsole",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(
                System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
                System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f))),
            BbcodeEnabled = true,
            ScrollFollowing = true,
            Modulate = UiStyle.Parchment(),
        };
        _panel.AddChild(_background);
        _panel.AddChild(_fade);
        _panel.AddChild(_textLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _textLabel is null || _background is null || _fade is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        var gameplayVisible = _gameManager?.CurrentState != GameManager.GameState.MainMenu && !_suppressedByOverlay;

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = new Vector2(OuterMargin, viewportSize.Y - panelSize.Y - OuterMargin);
        _background.Position = Vector2.Zero;
        _background.Size = panelSize;
        _fade.Position = Vector2.Zero;
        _fade.Size = new Vector2(panelSize.X, 16f);
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
