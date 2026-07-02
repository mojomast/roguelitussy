using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class CombatLog : Control
{
    private const int MaxMessages = 100;
    private const int VisibleMessageCount = 8;
    private const int MaxDisplayMessageChars = 96;
    private const float PanelWidth = 500f;
    private const float PanelHeight = 220f;
    private const float OuterMargin = 20f;
    private const float PanelPadding = 12f;
    private readonly Queue<LogEntry> _messages = new();
    private LogFilter _activeFilter = LogFilter.All;
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

    private enum LogFilter
    {
        All,
        CombatOnly,
        LootOnly,
        SystemOnly,
    }

    private sealed record LogEntry(string Markup, LogCategory Category);

    public CombatLog()
    {
        Name = "CombatLog";
        Visible = true;
        RebuildRenderedText();
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void RefreshConsole()
    {
        RebuildRenderedText();
        RefreshVisualState();
    }

    public void CycleFilter()
    {
        _activeFilter = _activeFilter switch
        {
            LogFilter.All => LogFilter.CombatOnly,
            LogFilter.CombatOnly => LogFilter.LootOnly,
            LogFilter.LootOnly => LogFilter.SystemOnly,
            _ => LogFilter.All,
        };

        RebuildRenderedText();
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
            _eventBus.ExperienceGained -= OnExperienceGained;
            _eventBus.LeveledUp -= OnLeveledUp;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
            _eventBus.KillStreakChanged -= OnKillStreakChanged;
            _eventBus.CriticalHitDealt -= OnCriticalHitDealt;
            _eventBus.SynergyActivated -= OnSynergyActivated;
            _eventBus.ReputationChanged -= OnReputationChanged;
            _eventBus.FloorCleared -= OnFloorCleared;
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
        _eventBus.ExperienceGained += OnExperienceGained;
        _eventBus.LeveledUp += OnLeveledUp;
        _eventBus.CurrencyChanged += OnCurrencyChanged;
        _eventBus.KillStreakChanged += OnKillStreakChanged;
        _eventBus.CriticalHitDealt += OnCriticalHitDealt;
        _eventBus.SynergyActivated += OnSynergyActivated;
        _eventBus.ReputationChanged += OnReputationChanged;
        _eventBus.FloorCleared += OnFloorCleared;
        RefreshVisualState();
    }

    public IReadOnlyCollection<string> Messages => _messages.Select(message => message.Markup).ToArray();

    private void OnLogMessage(string message, LogCategory category)
    {
        AddMessage(message, category);
    }

    private void OnDamageDealt(DamageResult damage)
    {
        var attacker = ResolveName(damage.AttackerId);
        var defender = ResolveName(damage.DefenderId);
        var message = damage.IsMiss
            ? $"{attacker} misses {defender}."
            : damage.IsCritical
                ? $"CRITICAL HIT! {attacker} hits {defender} for {damage.FinalDamage} damage."
                : $"{attacker} hits {defender} for {damage.FinalDamage} damage.";
        AddMessage(message, damage.IsCritical ? LogCategory.Critical : damage.AttackerId == _gameManager?.World?.Player?.Id ? LogCategory.PlayerAction : LogCategory.EnemyAction);
    }

    private void OnEntityDied(EntityId entityId)
    {
        AddMessage($"{ResolveName(entityId)} dies.", entityId == _gameManager?.World?.Player?.Id ? LogCategory.Critical : LogCategory.PlayerAction);
    }

    private void OnItemPickedUp(EntityId entityId, ItemInstance item)
    {
        var content = _gameManager?.Content;
        if (content is not null && content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            var actorName = ItemRarityPresentation.EscapeBBCode(ResolveName(entityId));
            var itemName = ItemRarityPresentation.WrapDecoratedNameWithColor(template.DisplayName, template.Rarity);
            if (ItemRarityPresentation.IsHighlighted(template.Rarity))
            {
                var callout = ItemRarityPresentation.EscapeBBCode(ItemRarityPresentation.ResolvePickupCallout(template));
                AddMarkupMessage($"[color=cyan]{actorName} secures {callout}: [/color]{itemName}[color=cyan].[/color]", LogCategory.Loot);
                return;
            }

            AddMarkupMessage($"[color=cyan]{actorName} picks up [/color]{itemName}[color=cyan].[/color]", LogCategory.Loot);
            return;
        }

        AddMessage($"{ResolveName(entityId)} picks up {ResolveItemName(item.TemplateId)}.", LogCategory.Loot);
    }

    private void OnStatusEffectApplied(EntityId entityId, StatusEffectInstance effect)
    {
        AddMessage($"{ResolveName(entityId)} gains {effect.Type} ({effect.RemainingTurns}).", LogCategory.StatusEffect);
    }

    private void OnSaveCompleted(bool success)
    {
        AddMessage(success ? "Save completed." : "Save failed.", success ? LogCategory.System : LogCategory.Warning);
    }

    private void OnLoadCompleted(bool success)
    {
        AddMessage(success ? "Load completed." : "Load failed.", success ? LogCategory.System : LogCategory.Warning);
    }

    private void OnFloorChanged(int floor)
    {
        AddMessage($"Reached floor {floor}.", LogCategory.System);
    }

    private void OnExperienceGained(EntityId entityId, int amount, int total)
    {
        if (entityId == _gameManager?.World?.Player?.Id)
        {
            AddMessage($"Gained {amount} XP ({total} total).", LogCategory.PlayerAction);
        }
    }

    private void OnLeveledUp(EntityId entityId, int newLevel)
    {
        if (entityId == _gameManager?.World?.Player?.Id)
        {
            AddMessage($"Level up! Reached level {newLevel}.", LogCategory.Critical);
        }
    }

    private void OnCurrencyChanged(EntityId entityId, int gold)
    {
        if (entityId == _gameManager?.World?.Player?.Id)
        {
            AddMessage($"Gold: {gold}.", LogCategory.Loot);
        }
    }

    private void OnKillStreakChanged(EntityId entityId, int current, int highest)
    {
        if (entityId == _gameManager?.World?.Player?.Id && current >= 2)
        {
            AddMessage($"Kill streak: {current} (best {highest}).", LogCategory.PlayerAction);
            if (current is 3 or 5 or 7)
            {
                AddMessage($"Momentum surges at streak {current}!", LogCategory.Critical);
            }
        }
    }

    private void OnCriticalHitDealt(EntityId attackerId, EntityId defenderId, int damage)
    {
        AddMessage($"CRITICAL HIT! {ResolveName(attackerId)} -> {ResolveName(defenderId)} ({damage}).", LogCategory.Critical);
    }

    private void OnSynergyActivated(SynergyDefinition synergy)
    {
        AddMessage($"{synergy.DisplayName} ACTIVATED!", LogCategory.Critical);
    }

    private void OnReputationChanged(string factionId, int newValue, int delta)
    {
        var arrow = delta >= 0 ? "↑" : "↓";
        AddMessage($"{arrow} {PrettifyFactionId(factionId)} ({delta:+#;-#;0}) now {newValue}.", delta < 0 ? LogCategory.Warning : LogCategory.System);
    }

    private void OnFloorCleared(int depth)
    {
        AddMessage($"Floor Cleared! +{10 + (depth * 5)} gold.", LogCategory.Critical);
    }

    private void AddMessage(string message, LogCategory category)
    {
        AddMarkupMessage(FormatEntryForTest(FitDisplayMessage(message), category, 0), category);
    }

    public static string FormatDisplayMessageForTest(string? message)
    {
        return FitDisplayMessage(message);
    }

    public static string FormatEntryForTest(string? message, LogCategory category, int age)
    {
        var color = GetCategoryColor(category);
        var alpha = ResolveAgeAlpha(age);
        var hex = alpha >= 0.995f ? UiStyle.ToHex(color) : ToHexWithAlpha(color, alpha);
        var escaped = ItemRarityPresentation.EscapeBBCode(message ?? string.Empty);
        var body = category == LogCategory.Critical ? $"[b]{escaped}[/b]" : escaped;
        return $"[color={hex}]{body}[/color]";
    }

    private static Color GetCategoryColor(LogCategory category) => category switch
    {
        LogCategory.PlayerAction => UiStyle.Parchment(),
        LogCategory.EnemyAction => UiStyle.DangerRed(),
        LogCategory.Loot => UiStyle.BrightGold(),
        LogCategory.StatusEffect => UiStyle.MutedText(),
        LogCategory.Warning => UiStyle.WarningAmber(),
        LogCategory.Critical => UiStyle.BrightGold(),
        _ => UiStyle.MutedText(),
    };

    private static float ResolveAgeAlpha(int age) => age > 6 ? 0.30f : age > 3 ? 0.60f : 1f;

    private static string FitDisplayMessage(string? message)
    {
        var text = message ?? string.Empty;
        return text.Length <= MaxDisplayMessageChars ? text : text[..(MaxDisplayMessageChars - 3)] + "...";
    }

    private static string ToHexWithAlpha(Color color, float alpha)
    {
        static int Channel(float value) => (int)System.Math.Clamp(System.MathF.Round(value * 255f), 0f, 255f);
        return $"#{Channel(color.R):x2}{Channel(color.G):x2}{Channel(color.B):x2}{Channel(alpha):x2}";
    }

    private void AddMarkupMessage(string markup, LogCategory category)
    {
        _messages.Enqueue(new LogEntry(markup, category));
        while (_messages.Count > MaxMessages)
        {
            _messages.Dequeue();
        }

        RebuildRenderedText();
        RefreshVisualState();
    }

    private void RebuildRenderedText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.MutedText())}][Filter: {GetFilterLabel(_activeFilter)}][/color]");

        var visible = _messages.Where(IsVisibleForActiveFilter).TakeLast(VisibleMessageCount).ToArray();
        for (var i = 0; i < visible.Length; i++)
        {
            var age = visible.Length - i - 1;
            var line = visible[i];
            var markupLine = age == 0 ? line.Markup : ApplyAgeFade(line.Markup, age, line.Category);
            builder.AppendLine(markupLine);
        }

        RenderedText = builder.ToString().TrimEnd();
    }

    private static string GetFilterLabel(LogFilter filter) => filter switch
    {
        LogFilter.CombatOnly => "Combat",
        LogFilter.LootOnly => "Loot",
        LogFilter.SystemOnly => "System",
        _ => "All",
    };

    private bool IsVisibleForActiveFilter(LogEntry entry) => _activeFilter switch
    {
        LogFilter.CombatOnly => entry.Category is LogCategory.PlayerAction
            or LogCategory.EnemyAction
            or LogCategory.Critical
            or LogCategory.StatusEffect
            or LogCategory.Warning,
        LogFilter.LootOnly => entry.Category == LogCategory.Loot,
        LogFilter.SystemOnly => entry.Category == LogCategory.System,
        _ => true,
    };

    private static string ApplyAgeFade(string markup, int age, LogCategory category)
    {
        var alpha = ResolveAgeAlpha(age);
        if (alpha >= 0.995f)
        {
            return markup;
        }

        return $"[color={ToHexWithAlpha(GetCategoryColor(category), alpha)}]{markup}[/color]";
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
            Modulate = UiStyle.GoldTrim(0.88f),
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

    private static string PrettifyFactionId(string factionId)
    {
        return string.Join(" ", factionId.Split('_').Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
