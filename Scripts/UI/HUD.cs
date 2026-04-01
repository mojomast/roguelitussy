using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class HUD : Control
{
    private Panel? _panel;
    private Label? _headerLabel;
    private Label? _progressLabel;
    private Label? _statsLabel;
    private Label? _effectsLabel;
    private Label? _mapLabel;
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public string HPText { get; private set; } = "HP: --/--";

    public string EnergyText { get; private set; } = "Energy: --";

    public string FloorText { get; private set; } = "Floor: --";

    public string TurnText { get; private set; } = "Turn: --";

    public string LevelText { get; private set; } = string.Empty;

    public string StatsText { get; private set; } = string.Empty;

    public string GoldText { get; private set; } = string.Empty;

    public string StatusEffectsText { get; private set; } = string.Empty;

    public string MinimapText { get; private set; } = "Map hidden";

    public Color HPColor { get; private set; } = Colors.White;

    public bool MinimapVisible { get; private set; } = true;

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

        builder.Append(MinimapText);
        return builder.ToString().TrimEnd();
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
        if (_gameManager?.World?.Player.Id == entityId)
        {
            HPText = $"HP: {currentHp}/{maxHp}";
            UpdateHPColor(currentHp, maxHp);
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

    private void Refresh()
    {
        EnsureVisualTree();

        var world = _gameManager?.World;
        if (world is null)
        {
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
            MinimapText = MinimapVisible
                ? $"Minimap: 0 explored, 0 visible"
                : "Minimap hidden";
            HPColor = Colors.White;
            UpdateLabels();
            return;
        }

        HPText = $"HP: {player.Stats.HP}/{player.Stats.MaxHP}";
        UpdateHPColor(player.Stats.HP, player.Stats.MaxHP);

        var energy = _gameManager?.Scheduler is TurnScheduler scheduler
            ? scheduler.GetEnergy(player.Id)
            : player.Stats.Energy;
        EnergyText = $"Energy: {energy}";
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

        var effects = StatusEffectProcessor.GetEffects(player);
        StatusEffectsText = effects.Count == 0
            ? string.Empty
            : "Effects: " + string.Join(", ", effects.Select(effect => $"{effect.Type}({effect.RemainingTurns})"));

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
        HPColor = ratio switch
        {
            > 0.6f => Colors.Green,
            > 0.3f => Colors.Yellow,
            _ => Colors.Red,
        };
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
            Position = new Vector2(12f, 12f),
            Size = new Vector2(440f, 132f),
        };
        AddChild(_panel);

        _headerLabel = CreateLabel("HeaderLabel", new Vector2(12f, 10f), new Vector2(416f, 18f));
        _progressLabel = CreateLabel("ProgressLabel", new Vector2(12f, 32f), new Vector2(416f, 18f));
        _statsLabel = CreateLabel("StatsLabel", new Vector2(12f, 54f), new Vector2(416f, 18f));
        _effectsLabel = CreateLabel("EffectsLabel", new Vector2(12f, 76f), new Vector2(416f, 18f));
        _mapLabel = CreateLabel("MapLabel", new Vector2(12f, 98f), new Vector2(416f, 18f));

        _panel.AddChild(_headerLabel);
        _panel.AddChild(_progressLabel);
        _panel.AddChild(_statsLabel);
        _panel.AddChild(_effectsLabel);
        _panel.AddChild(_mapLabel);
    }

    private void UpdateLabels()
    {
        if (_headerLabel is null || _progressLabel is null || _statsLabel is null || _effectsLabel is null || _mapLabel is null)
        {
            return;
        }

        _headerLabel.Text = $"{HPText}  {EnergyText}  {FloorText}  {TurnText}";
        _headerLabel.Modulate = HPColor;
        _progressLabel.Text = string.Join("  ", new[] { LevelText, GoldText }.Where(text => !string.IsNullOrWhiteSpace(text)));
        _statsLabel.Text = StatsText;
        _effectsLabel.Text = string.IsNullOrWhiteSpace(StatusEffectsText) ? "Effects: none" : StatusEffectsText;
        _mapLabel.Text = MinimapText;
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