using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class HUD : Control
{
    private Panel? _panel;
    private ColorRect? _panelBackground;
    private ColorRect? _panelBorderTop;
    private ColorRect? _panelBorderBottom;
    private ColorRect? _panelBorderLeft;
    private ColorRect? _panelBorderRight;
    private Label? _headerLabel;
    private Label? _hpLabel;
    private ProgressBar? _hpBarCompat;
    private Label? _hpValueLabel;
    private ColorRect? _hpBarBackground;
    private ColorRect? _hpBarFill;
    private Label? _energyLabel;
    private Label? _energyValueLabel;
    private ColorRect? _energyBarBackground;
    private ColorRect? _energyBarFill;
    private Label? _progressLabel;
    private Label? _statsLabel;
    private Label? _effectsLabel;
    private Label? _hotbarLabel;
    private HBoxContainer? _statusIconsContainer;
    private Label? _mapLabel;
    private UiMouseLabel? _interactionPromptLabel;
    private readonly List<ColorRect> _statPillBackgrounds = new();
    private readonly List<Label> _statPillLabels = new();
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public string HPText { get; private set; } = "HP: --/--";

    public double HPBarValue { get; private set; }

    public double HPBarMaxValue { get; private set; } = 1d;

    public double EnergyBarValue { get; private set; }

    public double EnergyBarMaxValue { get; private set; } = 1000d;

    public string EnergyText { get; private set; } = "Energy: --";

    public string FloorText { get; private set; } = "Floor: --";

    public string TurnText { get; private set; } = "Turn: --";

    public string LevelText { get; private set; } = string.Empty;

    public string StatsText { get; private set; } = string.Empty;

    public string GoldText { get; private set; } = string.Empty;

    public string StatusEffectsText { get; private set; } = string.Empty;

    public string HotbarText { get; private set; } = "Hotbar: empty";

    public string MinimapText { get; private set; } = "Map hidden";

    public string InteractionPromptText { get; private set; } = string.Empty;

    public Color HPColor { get; private set; } = Colors.White;

    public bool MinimapVisible { get; private set; } = true;

    public event System.Action? InteractionPromptActivated;

    public HUD()
    {
        Name = "HUD";
        EnsureVisualTree();
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.HPChanged -= OnHPChanged;
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.CurrencyChanged -= OnCurrencyChanged;
            _eventBus.ProgressionChanged -= OnProgressionChanged;
            _eventBus.InventoryChanged -= OnInventoryChanged;
            _eventBus.StatusEffectApplied -= OnStatusEffectChanged;
            _eventBus.StatusEffectRemoved -= OnStatusEffectChanged;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.FloorChanged += OnFloorChanged;
            _eventBus.HPChanged += OnHPChanged;
            _eventBus.LoadCompleted += OnLoadCompleted;
            _eventBus.CurrencyChanged += OnCurrencyChanged;
            _eventBus.ProgressionChanged += OnProgressionChanged;
            _eventBus.InventoryChanged += OnInventoryChanged;
            _eventBus.StatusEffectApplied += OnStatusEffectChanged;
            _eventBus.StatusEffectRemoved += OnStatusEffectChanged;
        }

        Refresh();
    }

    public void ToggleMinimap()
    {
        MinimapVisible = !MinimapVisible;
        Refresh();
    }

    public string Snapshot()
    {
        var builder = new StringBuilder();
        builder.AppendLine(HPText);
        builder.AppendLine(EnergyText);
        builder.AppendLine(FloorText);
        builder.AppendLine(TurnText);
        if (!string.IsNullOrWhiteSpace(StatsText))
        {
            builder.AppendLine(StatsText);
        }
        if (!string.IsNullOrWhiteSpace(GoldText))
        {
            builder.AppendLine(GoldText);
        }
        if (!string.IsNullOrWhiteSpace(LevelText))
        {
            builder.AppendLine(LevelText);
        }

        if (!string.IsNullOrWhiteSpace(StatusEffectsText))
        {
            builder.AppendLine(StatusEffectsText);
        }

        if (!string.IsNullOrWhiteSpace(HotbarText))
        {
            builder.AppendLine(HotbarText);
        }

        builder.Append(MinimapText);
        if (!string.IsNullOrWhiteSpace(InteractionPromptText))
        {
            builder.AppendLine();
            builder.Append(InteractionPromptText);
        }

        return builder.ToString().TrimEnd();
    }

    public void SetInteractionPrompt(string prompt)
    {
        InteractionPromptText = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim();
        UpdateInteractionPromptVisual();
    }

    private void OnTurnCompleted()
    {
        Refresh();
    }

    private void OnFloorChanged(int floor)
    {
        FloorText = $"Floor: {floor}";
        Refresh();
    }

    private void OnHPChanged(EntityId entityId, int currentHp, int maxHp)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            UpdateHPState(currentHp, maxHp);
            UpdateHPVisuals();
        }
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Refresh();
        }
    }

    private void OnCurrencyChanged(EntityId entityId, int gold)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            GoldText = $"Gold: {gold}";
            Refresh();
        }
    }

    private void OnProgressionChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            Refresh();
        }
    }

    private void OnInventoryChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            Refresh();
        }
    }

    private void OnStatusEffectChanged(EntityId entityId, StatusEffectInstance effect)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            Refresh();
        }
    }

    private void OnStatusEffectChanged(EntityId entityId, StatusEffectType effectType)
    {
        if (_gameManager?.World?.Player?.Id == entityId)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        EnsureVisualTree();

        var world = _gameManager?.World;
        if (world is null)
        {
            HPText = "HP: --/--";
            EnergyText = "Energy: --";
            FloorText = "Floor: --";
            TurnText = "Turn: --";
            LevelText = string.Empty;
            StatsText = string.Empty;
            GoldText = string.Empty;
            StatusEffectsText = string.Empty;
            HotbarText = "Hotbar: empty";
            MinimapText = MinimapVisible ? "Minimap: 0 explored, 0 visible" : "Minimap hidden";
            HPColor = Colors.White;
            HPBarValue = 0d;
            HPBarMaxValue = 1d;
            UpdateLabels();
            return;
        }

        var player = world.Player;
        if (player is null)
        {
            HPText = "HP: --/--";
            EnergyText = "Energy: --";
            FloorText = $"Floor: {world.Depth}";
            TurnText = $"Turn: {world.TurnNumber}";
            StatsText = string.Empty;
            GoldText = string.Empty;
            StatusEffectsText = string.Empty;
            HotbarText = "Hotbar: empty";
            MinimapText = MinimapVisible
                ? $"Minimap: 0 explored, 0 visible"
                : "Minimap hidden";
            HPColor = Colors.White;
            HPBarValue = 0d;
            HPBarMaxValue = 1d;
            UpdateLabels();
            return;
        }

        UpdateHPState(player.Stats.HP, player.Stats.MaxHP);

        var energy = _gameManager?.Scheduler is TurnScheduler scheduler
            ? scheduler.GetEnergy(player.Id)
            : player.Stats.Energy;
        EnergyText = $"Energy: {energy}";
        EnergyBarValue = System.Math.Clamp(energy, 0, 1000);
        EnergyBarMaxValue = 1000d;
        FloorText = $"Floor: {world.Depth}";
        TurnText = $"Turn: {world.TurnNumber}";

        var progression = player.GetComponent<ProgressionComponent>();
        var wallet = player.GetComponent<WalletComponent>();
        GoldText = wallet is null ? string.Empty : $"Gold: {wallet.Gold}";
        LevelText = progression is not null
            ? (progression.UnspentStatPoints > 0 || progression.UnspentPerkChoices > 0
                ? $"Lv: {progression.Level}  XP: {progression.Experience}/{progression.ExperienceToNextLevel}  LV UP!"
                : $"Lv: {progression.Level}  XP: {progression.Experience}/{progression.ExperienceToNextLevel}")
            : string.Empty;
        StatsText = BuildStatsText(player, progression);
        HotbarText = BuildHotbarText(world, player.Id, _gameManager?.Content);

        var effects = StatusEffectProcessor.GetEffects(player);
        StatusEffectsText = effects.Count == 0
            ? string.Empty
            : "Effects: " + string.Join(", ", effects.Select(effect => $"{effect.Type}({effect.RemainingTurns})"));
        SyncStatusIcons(effects);

        var visibleTiles = 0;
        var exploredTiles = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.IsVisible(position))
                {
                    visibleTiles++;
                }

                if (world.IsExplored(position))
                {
                    exploredTiles++;
                }
            }
        }

        MinimapText = MinimapVisible
            ? $"Minimap: {exploredTiles} explored, {visibleTiles} visible"
            : "Minimap hidden";

        UpdateLabels();
    }

    private void UpdateHPColor(int currentHp, int maxHp)
    {
        var ratio = maxHp <= 0 ? 0f : (float)currentHp / maxHp;
        HPColor = UiStyle.HpColor(ratio);
    }

    private void UpdateHPState(int currentHp, int maxHp)
    {
        HPText = $"HP: {currentHp}/{maxHp}";
        HPBarMaxValue = System.Math.Max(1d, maxHp);
        HPBarValue = System.Math.Clamp((double)currentHp, 0d, HPBarMaxValue);
        UpdateHPColor(currentHp, maxHp);
    }

    private void EnsureVisualTree()
    {
        if (_panel is not null)
        {
            return;
        }

        _panel = new Panel
        {
            Name = "Panel",
            Position = new Vector2(8f, 8f),
            Modulate = UiStyle.GoldTrim(),
        };
        AddChild(_panel);

        _panelBackground = new ColorRect { Name = "PanelBackground", Color = UiStyle.PanelBlack(0.80f) };
        _panelBorderTop = new ColorRect { Name = "PanelBorderTop", Color = UiStyle.BorderSubtle() };
        _panelBorderBottom = new ColorRect { Name = "PanelBorderBottom", Color = UiStyle.BorderSubtle() };
        _panelBorderLeft = new ColorRect { Name = "PanelBorderLeft", Color = UiStyle.BorderSubtle() };
        _panelBorderRight = new ColorRect { Name = "PanelBorderRight", Color = UiStyle.BorderSubtle() };
        _panel.AddChild(_panelBackground);
        _panel.AddChild(_panelBorderTop);
        _panel.AddChild(_panelBorderBottom);
        _panel.AddChild(_panelBorderLeft);
        _panel.AddChild(_panelBorderRight);

        _hpLabel = CreateLabel("HPLabel", new Vector2(12f, 10f), new Vector2(28f, 16f));
        _hpBarCompat = new ProgressBar { Name = "HPBar", MinValue = 0d, MaxValue = 1d, Value = 0d, Visible = false };
        _hpValueLabel = CreateLabel("HPValueLabel", new Vector2(170f, 10f), new Vector2(70f, 16f));
        _hpBarBackground = new ColorRect { Name = "HPBarBackground", Color = UiStyle.PanelBlack() };
        _hpBarFill = new ColorRect { Name = "HPBarFill", Color = UiStyle.ActiveGreen() };
        _energyLabel = CreateLabel("EnergyLabel", new Vector2(12f, 32f), new Vector2(28f, 16f));
        _energyValueLabel = CreateLabel("EnergyValueLabel", new Vector2(170f, 32f), new Vector2(70f, 16f));
        _energyBarBackground = new ColorRect { Name = "EnergyBarBackground", Color = UiStyle.PanelBlack() };
        _energyBarFill = new ColorRect { Name = "EnergyBarFill", Color = UiStyle.EnergyBlue() };
        _headerLabel = CreateLabel("HeaderLabel", new Vector2(12f, 46f), new Vector2(416f, 18f));
        _progressLabel = CreateLabel("ProgressLabel", new Vector2(12f, 68f), new Vector2(416f, 18f));
        _statsLabel = CreateLabel("StatsLabel", new Vector2(12f, 90f), new Vector2(416f, 18f));
        _effectsLabel = CreateLabel("EffectsLabel", new Vector2(12f, 112f), new Vector2(416f, 18f));
        _hotbarLabel = CreateLabel("HotbarLabel", new Vector2(12f, 134f), new Vector2(416f, 18f));
        _mapLabel = CreateLabel("MapLabel", new Vector2(12f, 156f), new Vector2(416f, 18f));
        _interactionPromptLabel = new UiMouseLabel
        {
            Name = "InteractionPromptLabel",
            Modulate = UiStyle.BrightGold(),
            InputSubmitted = input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    InteractionPromptActivated?.Invoke();
                }
            },
        };

        CreateStatPills();

        _panel.AddChild(_hpLabel);
        _panel.AddChild(_hpBarCompat);
        _panel.AddChild(_hpValueLabel);
        _panel.AddChild(_hpBarBackground);
        _panel.AddChild(_hpBarFill);
        _panel.AddChild(_energyLabel);
        _panel.AddChild(_energyValueLabel);
        _panel.AddChild(_energyBarBackground);
        _panel.AddChild(_energyBarFill);
        _panel.AddChild(_headerLabel);
        _panel.AddChild(_progressLabel);
        _panel.AddChild(_statsLabel);
        _panel.AddChild(_effectsLabel);
        _panel.AddChild(_hotbarLabel);

        _statusIconsContainer = new HBoxContainer
        {
            Name = "StatusIconsContainer",
            Position = new Vector2(12f, 130f),
            Size = new Vector2(416f, 24f),
        };
        _panel.AddChild(_statusIconsContainer);

        _panel.AddChild(_mapLabel);
        AddChild(_interactionPromptLabel);
        ApplyResponsiveLayout();
    }

    private void CreateStatPills()
    {
        if (_panel is null || _statPillBackgrounds.Count > 0)
        {
            return;
        }

        for (var i = 0; i < 6; i++)
        {
            var background = new ColorRect { Name = $"StatPillBackground_{i}", Color = UiStyle.SlotBackground() };
            var label = new Label { Name = $"StatPillLabel_{i}", Modulate = UiStyle.Parchment() };
            _statPillBackgrounds.Add(background);
            _statPillLabels.Add(label);
            _panel.AddChild(background);
            _panel.AddChild(label);
        }
    }

    private void SyncStatusIcons(IReadOnlyList<StatusEffectInstance> effects)
    {
        EnsureVisualTree();
        if (_statusIconsContainer is null)
        {
            return;
        }

        foreach (var child in _statusIconsContainer.GetChildren().ToArray())
        {
            _statusIconsContainer.RemoveChild(child);
            child.QueueFree();
        }

        var content = _gameManager?.Content;
        foreach (var effect in effects)
        {
            var statusId = StatusEffectIdFromType(effect.Type);
            if (string.IsNullOrWhiteSpace(statusId) || content?.TryGetStatusEffect(statusId, out var definition) != true || definition is null)
            {
                continue;
            }

            var iconTexture = LoadStatusIcon(definition.IconPath);
            if (iconTexture is not null)
            {
                var icon = new TextureRect
                {
                    Name = $"StatusIcon_{statusId}",
                    Texture = iconTexture,
                    Modulate = ParseTint(definition.ColorTint),
                    CustomMinimumSize = new Vector2(18f, 18f),
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                _statusIconsContainer.AddChild(icon);
            }

            var turnLabel = new Label
            {
                Name = $"StatusTurns_{statusId}",
                Text = effect.RemainingTurns.ToString(),
                Modulate = UiStyle.Parchment(),
            };
            _statusIconsContainer.AddChild(turnLabel);
        }
    }

    private static Texture2D? LoadStatusIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        try
        {
            return GD.Load<Texture2D>(iconPath);
        }
        catch
        {
            return null;
        }
    }

    private static Color ParseTint(string colorTint)
    {
        if (string.IsNullOrWhiteSpace(colorTint))
        {
            return Colors.White;
        }

        var parsed = ParseHtmlColor(colorTint);
        return parsed ?? Colors.White;
    }

    private static Color? ParseHtmlColor(string value)
    {
        var span = value.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length == 6 && TryParseHexByte(span[..2], out var r6) && TryParseHexByte(span[2..4], out var g6) && TryParseHexByte(span[4..6], out var b6))
        {
            return new Color(r6 / 255f, g6 / 255f, b6 / 255f, 1f);
        }

        if (span.Length == 8 && TryParseHexByte(span[..2], out var r8) && TryParseHexByte(span[2..4], out var g8) && TryParseHexByte(span[4..6], out var b8) && TryParseHexByte(span[6..8], out var a8))
        {
            return new Color(r8 / 255f, g8 / 255f, b8 / 255f, a8 / 255f);
        }

        return null;
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> chars, out byte value)
    {
        value = 0;
        if (chars.Length != 2)
        {
            return false;
        }

        return byte.TryParse(chars, System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    private static string? StatusEffectIdFromType(StatusEffectType type) => type switch
    {
        StatusEffectType.Poisoned => "poisoned",
        StatusEffectType.Burning => "burning",
        StatusEffectType.Frozen => "frozen",
        StatusEffectType.Stunned => "stunned",
        StatusEffectType.Hasted => "haste",
        StatusEffectType.Invisible => null,
        StatusEffectType.Regenerating => "regenerating",
        StatusEffectType.Weakened => "weakened",
        StatusEffectType.Shielded => "shielded",
        StatusEffectType.Empowered => "empowered",
        StatusEffectType.Corroded => "corroded",
        StatusEffectType.Phased => "phased",
        StatusEffectType.Flying => "flying",
        _ => null,
    };

    private void UpdateLabels()
    {
        ApplyResponsiveLayout();
        UpdateHPVisuals();

        if (_headerLabel is null || _progressLabel is null || _statsLabel is null || _effectsLabel is null || _hotbarLabel is null || _mapLabel is null)
        {
            return;
        }

        _headerLabel.Text = $"{FloorText}  ▪  {TurnText}  ▪  {LevelText}  ▪  {GoldText}".Trim();
        _headerLabel.Modulate = UiStyle.Parchment();
        _progressLabel.Text = string.Join("  ", new[] { LevelText, GoldText }.Where(text => !string.IsNullOrWhiteSpace(text)));
        _progressLabel.Modulate = LevelText.Contains("LV UP!", System.StringComparison.Ordinal) ? UiStyle.BrightGold() : UiStyle.MutedText();
        _statsLabel.Text = StatsText;
        _statsLabel.Modulate = UiStyle.Parchment();
        _statsLabel.Visible = false;
        UpdateStatPills();
        _effectsLabel.Text = StatusEffectsText;
        _effectsLabel.Visible = !string.IsNullOrWhiteSpace(StatusEffectsText);
        _effectsLabel.Modulate = UiStyle.WarningOrange();
        _hotbarLabel.Text = HotbarText;
        _hotbarLabel.Modulate = UiStyle.BrightGold();
        _mapLabel.Text = MinimapText;
        _mapLabel.Modulate = UiStyle.FaintText();
        UpdateInteractionPromptVisual();
    }

    private void UpdateHPVisuals()
    {
        EnsureVisualTree();

        if (_hpLabel is not null)
        {
            _hpLabel.Text = HPText;
            _hpLabel.Modulate = HPColor;
        }

        if (_hpBarCompat is not null)
        {
            _hpBarCompat.MinValue = 0d;
            _hpBarCompat.MaxValue = HPBarMaxValue;
            _hpBarCompat.Value = HPBarValue;
            _hpBarCompat.Modulate = HPColor;
        }

        if (_hpValueLabel is not null)
        {
            _hpValueLabel.Text = HPText.Replace("HP: ", string.Empty);
            _hpValueLabel.Modulate = HPColor;
        }

        if (_energyLabel is not null)
        {
            _energyLabel.Text = "EN";
            _energyLabel.Modulate = UiStyle.Parchment();
        }

        if (_energyValueLabel is not null)
        {
            _energyValueLabel.Text = EnergyText.Replace("Energy: ", string.Empty);
            _energyValueLabel.Modulate = UiStyle.EnergyBlue();
        }

        LayoutBar(_hpBarBackground, _hpBarFill, HPBarValue, HPBarMaxValue, HPColor);
        LayoutBar(_energyBarBackground, _energyBarFill, EnergyBarValue, EnergyBarMaxValue, UiStyle.EnergyBlue(), y: 37f);
    }

    private void UpdateStatPills()
    {
        var parts = StatsText.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < _statPillLabels.Count; i++)
        {
            var label = _statPillLabels[i];
            label.Visible = parts.Length >= (i * 2) + 2;
            if (!label.Visible)
            {
                continue;
            }

            label.Text = $"{parts[i * 2]} {parts[(i * 2) + 1]}";
            label.Modulate = UiStyle.Parchment();
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (_panel is null)
        {
            return;
        }

        var viewportWidth = ResolveViewportWidth();
        var width = System.Math.Min(440f, System.Math.Max(0f, viewportWidth - 24f));
        var contentWidth = System.Math.Max(0f, width - 24f);
        _panel.Position = new Vector2(8f, 8f);
        _panel.Size = new Vector2(width, 190f);
        _panel.Modulate = UiStyle.GoldTrim();
        LayoutPanelChrome(_panel.Size);

        SetControlBounds(_hpLabel, 12f, 10f, 28f, 16f);
        SetControlBounds(_hpValueLabel, 170f, 10f, 70f, 16f);
        SetControlBounds(_energyLabel, 12f, 32f, 28f, 16f);
        SetControlBounds(_energyValueLabel, 170f, 32f, 70f, 16f);
        SetControlBounds(_headerLabel, 12f, 56f, contentWidth, 18f);
        SetControlBounds(_progressLabel, 12f, 76f, contentWidth, 18f);
        SetControlBounds(_statsLabel, 12f, 96f, contentWidth, 18f);
        LayoutStatPills(12f, 96f, contentWidth);
        SetControlBounds(_effectsLabel, 12f, 116f, contentWidth, 18f);
        SetControlBounds(_hotbarLabel, 12f, 134f, contentWidth, 18f);
        if (_statusIconsContainer is not null)
        {
            _statusIconsContainer.Position = new Vector2(12f, 152f);
            _statusIconsContainer.Size = new Vector2(contentWidth, 22f);
        }

        SetControlBounds(_mapLabel, 12f, 172f, contentWidth, 18f);
        LayoutInteractionPrompt();
    }

    private void LayoutInteractionPrompt()
    {
        if (_interactionPromptLabel is null)
        {
            return;
        }

        var viewportWidth = ResolveViewportWidth();
        var viewportHeight = ResolveViewportHeight();
        var width = System.Math.Min(360f, System.Math.Max(180f, viewportWidth * 0.34f));
        _interactionPromptLabel.Position = new Vector2(System.Math.Max(8f, (viewportWidth - width) * 0.5f), System.Math.Max(120f, viewportHeight - 148f));
        _interactionPromptLabel.Size = new Vector2(width, 24f);
    }

    private void UpdateInteractionPromptVisual()
    {
        if (_interactionPromptLabel is null)
        {
            return;
        }

        LayoutInteractionPrompt();
        _interactionPromptLabel.Visible = !string.IsNullOrWhiteSpace(InteractionPromptText);
        _interactionPromptLabel.Text = InteractionPromptText;
        _interactionPromptLabel.Modulate = UiStyle.BrightGold();
    }

    private void LayoutPanelChrome(Vector2 size)
    {
        if (_panelBackground is null || _panelBorderTop is null || _panelBorderBottom is null || _panelBorderLeft is null || _panelBorderRight is null)
        {
            return;
        }

        _panelBackground.Position = Vector2.Zero;
        _panelBackground.Size = size;
        _panelBackground.Color = UiStyle.PanelBlack(0.80f);
        _panelBorderTop.Position = Vector2.Zero;
        _panelBorderTop.Size = new Vector2(size.X, 1f);
        _panelBorderBottom.Position = new Vector2(0f, size.Y - 1f);
        _panelBorderBottom.Size = new Vector2(size.X, 1f);
        _panelBorderLeft.Position = Vector2.Zero;
        _panelBorderLeft.Size = new Vector2(1f, size.Y);
        _panelBorderRight.Position = new Vector2(size.X - 1f, 0f);
        _panelBorderRight.Size = new Vector2(1f, size.Y);
    }

    private void LayoutStatPills(float x, float y, float width)
    {
        var pillWidth = System.Math.Max(44f, (width - 15f) / 6f);
        for (var i = 0; i < _statPillBackgrounds.Count; i++)
        {
            var px = x + (i * (pillWidth + 3f));
            _statPillBackgrounds[i].Position = new Vector2(px, y);
            _statPillBackgrounds[i].Size = new Vector2(pillWidth, 18f);
            _statPillBackgrounds[i].Color = UiStyle.SlotBackground();
            _statPillLabels[i].Position = new Vector2(px + 4f, y + 1f);
            _statPillLabels[i].Size = new Vector2(System.Math.Max(0f, pillWidth - 8f), 16f);
        }
    }

    private static void LayoutBar(ColorRect? background, ColorRect? fill, double value, double maxValue, Color fillColor, float y = 15f)
    {
        if (background is null || fill is null)
        {
            return;
        }

        const float width = 120f;
        const float height = 8f;
        background.Position = new Vector2(44f, y);
        background.Size = new Vector2(width, height);
        background.Color = UiStyle.SlotBackground();
        var fraction = maxValue <= 0d ? 0f : (float)System.Math.Clamp(value / maxValue, 0d, 1d);
        fill.Position = background.Position + new Vector2(1f, 1f);
        fill.Size = new Vector2((width - 2f) * fraction, height - 2f);
        fill.Color = fillColor;
    }

    private static void SetControlBounds(Control? control, float x, float y, float width, float height)
    {
        if (control is null)
        {
            return;
        }

        control.Position = new Vector2(x, y);
        control.Size = new Vector2(width, height);
    }

    private float ResolveViewportWidth()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size.X : 1280f;
    }

    private float ResolveViewportHeight()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size.Y : 720f;
    }

    private static string BuildStatsText(IEntity player, ProgressionComponent? progression)
    {
        var builder = new StringBuilder();
        builder.Append($"ATK {player.Stats.Attack}  DEF {player.Stats.Defense}  ACC {player.Stats.Accuracy}  EVA {player.Stats.Evasion}  SPD {player.Stats.Speed}  VIS {player.Stats.ViewRadius}");
        if (progression is not null)
        {
            if (progression.UnspentStatPoints > 0)
            {
                builder.Append($"  PTS {progression.UnspentStatPoints}");
            }

            if (progression.UnspentPerkChoices > 0)
            {
                builder.Append($"  PERKS {progression.UnspentPerkChoices}");
            }
        }

        return builder.ToString();
    }

    private static string BuildHotbarText(IWorldState world, EntityId playerId, IContentDatabase? content)
    {
        var items = UIActionFactory.GetQuickUseItems(world, content, playerId);
        if (items.Count == 0 || content is null)
        {
            return "Hotbar: empty";
        }

        var parts = new List<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var label = content.TryGetItemTemplate(item.TemplateId, out var template)
                ? template.DisplayName
                : item.TemplateId;
            if (item.StackCount > 1)
            {
                label += $" x{item.StackCount}";
            }

            if (template?.RequiresTargetSelection == true)
            {
                label += " (aim)";
            }

            parts.Add($"{i + 1}:{label}");
        }

        return "Hotbar: " + string.Join("  ", parts);
    }

    private static Label CreateLabel(string name, Vector2 position, Vector2 size)
    {
        return new Label
        {
            Name = name,
            Position = position,
            Size = size,
        };
    }
}
