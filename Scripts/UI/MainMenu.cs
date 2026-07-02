using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class MainMenu : MenuBase
{
    private const float PreviewPadding = 20f;
    private const float PreviewFrameMinimumHeight = 144f;

    private enum MenuAction
    {
        None,
        Start,
        Name,
        Seed,
        Archetype,
        Origin,
        Trait,
        Race,
        Gender,
        Appearance,
        Vitality,
        Power,
        Guard,
        Finesse,
        LoadSlot1,
        LoadSlot2,
        LoadSlot3,
        DailyChallenge,
        MetaShop,
        DevTools,
        Help,
        Quit,
    }

    private sealed record StatBonus(
        int MaxHp,
        int Attack,
        int Defense,
        int Accuracy,
        int Evasion,
        int Speed,
        int ViewRadius,
        int InventoryCapacity);

    private sealed record ArchetypeOption(
        string DisplayName,
        string ArchetypeId,
        string Summary,
        string SignatureMechanic,
        StatBonus Bonus,
        IReadOnlyList<string> StartingItems,
        IReadOnlyList<string> EquippedItems);

    private sealed record OriginOption(
        string DisplayName,
        string Summary,
        StatBonus Bonus,
        IReadOnlyList<string> StartingItems);

    private sealed record TraitOption(
        string DisplayName,
        string Summary,
        StatBonus Bonus,
        IReadOnlyList<string> StartingItems);

    private static readonly string[] NameOptions =
    {
        "Rook",
        "Iris",
        "Nyx",
        "Bram",
        "Mara",
        "Orin",
    };

    private static readonly string[] RaceOptions = { "Human", "Elf", "Dwarf", "Orc" };
    private static readonly string[] GenderOptions = { "Neutral", "Masculine", "Feminine" };
    private static readonly string[] AppearanceOptions = { "Default", "Scarred", "Youthful", "Weathered" };

    private static readonly ArchetypeOption[] Archetypes =
    {
        new(
            "Vanguard",
            "vanguard",
            "A tough delver who starts armed and ready for the front line.",
            "Shield Bash: control space and endure punishment.",
            new StatBonus(10, 0, 2, -5, -2, -10, -1, 0),
            new[] { "potion_health", "potion_health", "item_shield_basic" },
            new[] { "item_shield_basic" }),
        new(
            "Ranger",
            "ranger",
            "A mobile marksman with accurate ranged pressure.",
            "Ranged Shot: spend arrows to strike from safety.",
            new StatBonus(-5, 2, -1, 15, 8, 10, 2, 0),
            new[] { "potion_health", "item_arrows_bundle" },
            System.Array.Empty<string>()),
        new(
            "Trickster",
            "trickster",
            "A slippery killer who snowballs chained takedowns.",
            "Kill Streak: bonus pressure after repeated kills.",
            new StatBonus(-10, 1, -2, 5, 12, 20, 1, 0),
            new[] { "potion_health", "item_smoke_bomb" },
            System.Array.Empty<string>()),
        new(
            "Arcanist",
            "arcanist",
            "A fragile caster who starts with scrolls and arcane abilities.",
            "Arcane Charges: native spells backed by scroll burst.",
            new StatBonus(-12, -2, -2, 10, 2, 0, 1, 0),
            new[] { "scroll_fireball", "scroll_frost_nova", "potion_mana" },
            System.Array.Empty<string>()),
    };

    private static readonly OriginOption[] Origins =
    {
        new(
            "Survivor",
            "Veteran of too many bad runs. Starts a little tougher.",
            new StatBonus(4, 0, 1, 0, 0, 0, 0, 0),
            new[] { "potion_health" }),
        new(
            "Scout",
            "Maps routes quickly and sees danger earlier.",
            new StatBonus(0, 0, 0, 0, 0, 5, 1, 0),
            new[] { "potion_haste" }),
        new(
            "Scavenger",
            "Carries more, wastes less, and hoards every edge.",
            new StatBonus(0, 0, 0, 0, 0, 0, 0, 4),
            new[] { "potion_health", "scroll_blink" }),
    };

    private static readonly TraitOption[] Traits =
    {
        new(
            "Iron Will",
            "Push through attrition with a deeper life pool and sturdier posture.",
            new StatBonus(4, 0, 1, 0, 0, 0, 0, 0),
            new[] { "potion_health" }),
        new(
            "Quartermaster",
            "Travel heavier and keep extra supplies close at hand.",
            new StatBonus(0, 0, 0, 0, 0, 0, 0, 2),
            new[] { "potion_health", "potion_health" }),
        new(
            "Pathfinder",
            "See farther, move quicker, and carry a panic-scroll.",
            new StatBonus(0, 0, 0, 0, 0, 5, 1, 0),
            new[] { "scroll_blink" }),
    };

    private const int AllocationBudget = 4;

    private const int StartIndex = 0;
    private const int NameIndex = 1;
    private const int ArchetypeIndex = 2;
    private const int OriginIndex = 3;
    private const int TraitIndex = 4;
    private const int RaceIndex = 5;
    private const int GenderIndex = 6;
    private const int AppearanceIndex = 7;
    private const int VitalityIndex = 8;
    private const int PowerIndex = 9;
    private const int GuardIndex = 10;
    private const int FinesseIndex = 11;
    private const int SeedIndex = 12;
    private const int DevToolsIndex = 13;
    private const int HelpIndex = 14;
    private const int LoadSlot1Index = 15;
    private const int LoadSlot2Index = 16;
    private const int LoadSlot3Index = 17;
    private const int QuitIndex = 18;

    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private MetaProgressionManager? _metaProgression;
    private DailyChallengeManager? _dailyChallenge;
    private readonly List<MenuAction> _optionActions = new();
    private int _nameIndex;
    private int _archetypeIndex;
    private int _originIndex;
    private int _traitIndex;
    private int _vitalityPoints;
    private int _powerPoints;
    private int _guardPoints;
    private int _finessePoints;
    private int _raceIndex;
    private int _genderIndex;
    private int _appearanceIndex;
    private Panel? _previewPanel;
    private ColorRect? _previewFrame;
    private TextureRect? _previewBody;
    private ColorRect? _previewAccentBand;
    private Label? _previewSigil;
    private Label? _previewDetail;
    private Label? _previewKitLabel;
    private Label? _previewStatsLabel;
    private Label? _previewTitle;
    private Label? _previewSubtitle;
    private Label? _previewVariantId;
    private Tooltip? _starterKitTooltip;
    private bool _starterKitTooltipVisible;
    private bool _isEditingSeed;
    private string _seedEditBuffer = "1337";
    private string _statusMessage = "Ready for deployment.";

    public int PendingSeed { get; private set; } = 1337;

    public event System.Action? GameStarted;

    public event System.Action? HelpRequested;

    public event System.Action? DevToolsRequested;

    public event System.Action? MetaShopRequested;

    public MainMenu()
    {
        Name = "MainMenu";
        Title = "GODOTUSSY ROGUELIKE";
        RebuildOptions();
        Visible = true;
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        Bind(gameManager, eventBus, null);
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, MetaProgressionManager? metaProgression)
    {
        Bind(gameManager, eventBus, metaProgression, null);
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, MetaProgressionManager? metaProgression, DailyChallengeManager? dailyChallenge)
    {
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.LogMessage -= OnLogMessage;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _metaProgression = metaProgression;
        _dailyChallenge = dailyChallenge;
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted += OnLoadCompleted;
            _eventBus.LogMessage += OnLogMessage;
        }

        if (_gameManager?.Seed > 0)
        {
            PendingSeed = _gameManager.Seed;
        }

        RebuildOptions();
    }

    public void SetSeed(int seed)
    {
        PendingSeed = seed <= 0 ? 1 : seed;
        _seedEditBuffer = PendingSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _isEditingSeed = false;
        RebuildOptions();
    }

    protected override string BuildBodyText()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        return string.Join(
            "\n",
            "EXPEDITION",
            $"Candidate: {NameOptions[_nameIndex]}  Seed: {PendingSeed}",
            $"Build: {FormatArchetypeName(archetype)} / {origin.DisplayName} / {trait.DisplayName}",
            $"Identity: {RaceOptions[_raceIndex]} / {GenderOptions[_genderIndex]} / {AppearanceOptions[_appearanceIndex]}",
            $"Training: VIT {_vitalityPoints}  POW {_powerPoints}  GRD {_guardPoints}  FIN {_finessePoints}",
            $"Points Remaining: {RemainingPoints}",
            $"Signature: {archetype.SignatureMechanic}",
            "Training effects: VIT +3 Max HP, POW +1 Attack, GRD +1 Defense, FIN +1 Accuracy and +1 Evasion.",
            "Use Left/Right or +/- to edit the highlighted field.");
    }

    protected override string BuildFooterText()
    {
        return "Enter deploy  Arrows or +/- adjust  Tab starter kit  H help  T workshop";
    }

    public override void Close()
    {
        HideStarterKitTooltip();
        base.Close();
    }

    public string BuildStatPreview()
    {
        var (hp, atk, def, acc, eva, spd, vr) = ComputeProjectedStats();

        return string.Join(
            "\n",
            "--- Stat Preview ---",
            $"HP: {hp}  ATK: {atk}  DEF: {def}",
            $"ACC: {acc}  EVA: {eva}  SPD: {spd}  VR: {vr}");
    }

    public string BuildIdentityPreview()
    {
        var profile = ResolveCurrentProfile();
        return string.Join(
            "\n",
            "--- Identity Preview ---",
            $"Sprite: {profile.Title}",
            $"Accent: {profile.Subtitle}",
            $"Token: {BuildPreviewTileToken()}",
            $"Variant ID: {profile.VariantId}");
    }

    public string BuildPreviewTileToken()
    {
        return PlayerVisualCatalog.BuildPreviewToken(ResolveCurrentProfile());
    }

    public static IEnumerable<string> EnumerateAuthoredStartingItemIds()
    {
        foreach (var archetype in Archetypes)
        {
            foreach (var itemId in archetype.StartingItems.Concat(archetype.EquippedItems))
            {
                yield return itemId;
            }
        }

        foreach (var origin in Origins)
        {
            foreach (var itemId in origin.StartingItems)
            {
                yield return itemId;
            }
        }

        foreach (var trait in Traits)
        {
            foreach (var itemId in trait.StartingItems)
            {
                yield return itemId;
            }
        }
    }

    private string BuildHeroSummary()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        return string.Join(
            "\n",
            $"Candidate  {NameOptions[_nameIndex]}",
            $"Loadout    {FormatArchetypeName(archetype)} / {origin.DisplayName} / {trait.DisplayName}",
            $"Identity   {RaceOptions[_raceIndex]} / {GenderOptions[_genderIndex]} / {AppearanceOptions[_appearanceIndex]}",
            $"Training   VIT {_vitalityPoints}  POW {_powerPoints}  GRD {_guardPoints}  FIN {_finessePoints}",
            $"Reserve    {RemainingPoints} point(s) left  Seed {PendingSeed}",
            BuildDailySummaryLine(),
            $"Status     {_statusMessage}");
    }

    public string BuildStarterKitPreviewText()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        var allItems = new List<string>();
        allItems.AddRange(archetype.StartingItems);
        allItems.AddRange(origin.StartingItems);
        allItems.AddRange(trait.StartingItems);
        var pack = SubtractItemCounts(allItems, archetype.EquippedItems);

        return string.Join(
            "\n",
            "READY KIT",
            "Equipped:",
            FormatStarterItemLines(archetype.EquippedItems),
            "Pack:",
            FormatStarterItemLines(pack));
    }

    private string BuildPreviewStatsText()
    {
        var projected = BuildProjectedStatLines();
        return string.Join("\n", "PROJECTED", projected[0], projected[1]);
    }

    private string BuildPreviewPathText()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        return $"Path: {FormatArchetypeName(archetype)} / {origin.DisplayName}\n{archetype.SignatureMechanic}";
    }

    private string FormatStarterItemLines(IEnumerable<string> itemIds)
    {
        var values = itemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .GroupBy(itemId => itemId)
            .Take(4)
            .Select(group => FormatStarterItemLine(group.Key, group.Count()))
            .ToArray();

        return values.Length == 0 ? "- none" : string.Join("\n", values);
    }

    private string FormatStarterItemLine(string itemId, int count)
    {
        var displayName = PrettifyTemplateId(itemId);
        var description = string.Empty;
        var requiresTargeting = false;
        if (_gameManager?.Content?.TryGetItemTemplate(itemId, out var template) == true)
        {
            displayName = string.IsNullOrWhiteSpace(template.DisplayName) ? displayName : template.DisplayName.Trim();
            description = NormalizePreviewDescription(template.Description);
            requiresTargeting = template.RequiresTargetSelection;
        }

        var countSuffix = count > 1 ? $" x{count}" : string.Empty;
        var parts = new List<string> { $"- {displayName}{countSuffix}" };
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description);
        }

        if (requiresTargeting)
        {
            parts.Add("Requires targeting.");
        }

        return string.Join(" - ", parts);
    }

    private static IReadOnlyList<string> SubtractItemCounts(IEnumerable<string> itemIds, IEnumerable<string> equippedItemIds)
    {
        var remainingEquipped = equippedItemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .GroupBy(itemId => itemId)
            .ToDictionary(group => group.Key, group => group.Count());
        var pack = new List<string>();
        foreach (var itemId in itemIds.Where(itemId => !string.IsNullOrWhiteSpace(itemId)))
        {
            if (remainingEquipped.TryGetValue(itemId, out var count) && count > 0)
            {
                remainingEquipped[itemId] = count - 1;
                continue;
            }

            pack.Add(itemId);
        }

        return pack;
    }

    private static string NormalizePreviewDescription(string description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : description.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string PrettifyTemplateId(string templateId)
    {
        return string.Join(
            " ",
            templateId
                .Split('_', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string WrapPreviewText(string text, float availableWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // TODO: replace this temporary approximation with Font.GetStringSize() once project fonts are finalized.
        var maxCharsPerLine = System.Math.Max(18, System.Math.Min(30, (int)System.Math.Floor(availableWidth / 7.25f)));
        var wrappedLines = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length <= maxCharsPerLine)
            {
                wrappedLines.Add(line);
                continue;
            }

            var words = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                wrappedLines.Add(string.Empty);
                continue;
            }

            var currentLine = words[0];
            for (var index = 1; index < words.Length; index++)
            {
                var next = words[index];
                if (currentLine.Length + 1 + next.Length > maxCharsPerLine)
                {
                    wrappedLines.Add(currentLine);
                    currentLine = next;
                }
                else
                {
                    currentLine += $" {next}";
                }
            }

            wrappedLines.Add(currentLine);
        }

        return string.Join("\n", wrappedLines);
    }

    private string[] BuildProjectedStatLines()
    {
        var (hp, atk, def, acc, eva, spd, vr) = ComputeProjectedStats();

        return new[]
        {
            $"HP {hp}  ATK {atk}  DEF {def}",
            $"ACC {acc}  EVA {eva}  SPD {spd}  VR {vr}",
        };
    }

    private (int hp, int atk, int def, int acc, int eva, int spd, int vr) ComputeProjectedStats()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        var hp = BaseMaxHp + archetype.Bonus.MaxHp + origin.Bonus.MaxHp + trait.Bonus.MaxHp + (_vitalityPoints * 3);
        var atk = BaseAttack + archetype.Bonus.Attack + origin.Bonus.Attack + trait.Bonus.Attack + _powerPoints;
        var def = BaseDefense + archetype.Bonus.Defense + origin.Bonus.Defense + trait.Bonus.Defense + _guardPoints;
        var acc = BaseAccuracy + archetype.Bonus.Accuracy + origin.Bonus.Accuracy + trait.Bonus.Accuracy + _finessePoints;
        var eva = BaseEvasion + archetype.Bonus.Evasion + origin.Bonus.Evasion + trait.Bonus.Evasion + _finessePoints;
        var spd = BaseSpeed + archetype.Bonus.Speed + origin.Bonus.Speed + trait.Bonus.Speed;
        var vr = BaseViewRadius + archetype.Bonus.ViewRadius + origin.Bonus.ViewRadius + trait.Bonus.ViewRadius;
        return (hp, atk, def, acc, eva, spd, vr);
    }

    protected override Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var baseSize = base.ResolveDesiredPanelSize(viewportSize);
        return new Vector2(System.Math.Max(baseSize.X, 920f), System.Math.Max(baseSize.Y, 660f));
    }

    protected override float ResolveApproxLineHeight()
    {
        var viewportHeight = GetParent() is not null && GetTree() is not null ? GetViewportRect().Size.Y : 720f;
        return viewportHeight >= 600f ? 12f : base.ResolveApproxLineHeight();
    }

    protected override void OnVisualStateRefreshed(Panel panel, RichTextLabel label, Vector2 viewportSize, Vector2 panelSize)
    {
        EnsurePreviewVisuals(panel);

        if (Backdrop is not null)
        {
            Backdrop.Color = UiStyle.PanelBlack(0.98f);
        }

        if (HeaderBand is not null)
        {
            HeaderBand.Color = UiStyle.PanelHighlight(0.98f);
            HeaderBand.Size = new Vector2(panelSize.X, 60f);
        }

        if (TitleLabel is not null)
        {
            TitleLabel.Text = "GODOTUSSY  //  DELVER FOUNDRY";
            TitleLabel.Position = new Vector2(PreviewPadding, 16f);
            TitleLabel.Size = new Vector2(System.Math.Max(0f, panelSize.X - (PreviewPadding * 2f)), 28f);
            TitleLabel.Modulate = UiStyle.BrightGold();
        }

        if (FooterLabel is not null)
        {
            FooterLabel.Text = FitLabelText(BuildFooterText(), FooterLabel.Size.X);
            FooterLabel.Position = new Vector2(PreviewPadding, panelSize.Y - 34f);
            FooterLabel.Size = new Vector2(System.Math.Max(0f, panelSize.X - (PreviewPadding * 2f)), 22f);
            FooterLabel.Modulate = UiStyle.MutedText();
        }

        if (BodyCard is null || OptionsCard is null || OptionsLabel is null)
        {
            return;
        }

        var contentTop = 78f;
        var footerTop = panelSize.Y - 46f;
        var contentHeight = System.Math.Max(0f, footerTop - contentTop);
        var totalContent = System.Math.Max(0f, panelSize.X - (PreviewPadding * 4f));
        var summaryWidth = totalContent * 0.26f;
        var optionsWidth = totalContent * 0.40f;
        var previewWidth = totalContent * 0.34f;

        BodyCard.Color = UiStyle.PanelInner(0.98f);
        BodyCard.Position = new Vector2(PreviewPadding, contentTop);
        BodyCard.Size = new Vector2(summaryWidth, contentHeight);

        label.Position = new Vector2(18f, 16f);
        label.Size = new Vector2(
            System.Math.Max(0f, BodyCard.Size.X - 36f),
            System.Math.Max(0f, BodyCard.Size.Y - 32f));
        label.Clear();
        label.AppendText(ClampTextLines(WrapPreviewText(BuildHeroSummary(), label.Size.X), 8));
        label.Modulate = UiStyle.Parchment();

        OptionsCard.Color = UiStyle.CathedralBlack(0.99f);
        OptionsCard.Position = new Vector2(PreviewPadding + summaryWidth + PreviewPadding, contentTop);
        OptionsCard.Size = new Vector2(optionsWidth, contentHeight);

        var visibleOptionsText = OptionsLabel.Text;
        OptionsLabel.Position = new Vector2(18f, 14f);
        OptionsLabel.Size = new Vector2(
            System.Math.Max(0f, OptionsCard.Size.X - 36f),
            System.Math.Max(0f, OptionsCard.Size.Y - 28f));
        OptionsLabel.Text = visibleOptionsText;
        OptionsLabel.Modulate = UiStyle.Parchment();

        if (_previewPanel is null)
        {
            return;
        }

        _previewPanel.Visible = Visible;
        _previewPanel.Position = new Vector2(OptionsCard.Position.X + OptionsCard.Size.X + PreviewPadding, contentTop);
        _previewPanel.Size = new Vector2(previewWidth, contentHeight);
        LayoutPreview(_previewPanel.Size);
        RefreshPreviewContent();
    }

    internal const int BaseMaxHp = 40;
    internal const int BaseAttack = 8;
    internal const int BaseDefense = 3;
    internal const int BaseAccuracy = 80;
    internal const int BaseEvasion = 10;
    internal const int BaseSpeed = 100;
    internal const int BaseViewRadius = 8;

    public override bool HandleKey(Key key)
    {
        if (_isEditingSeed && Visible)
        {
            return HandleSeedEditKey(key);
        }

        return base.HandleKey(key);
    }

    protected override bool HandleCustomKey(Key key)
    {
        switch (key)
        {
            case Key.Left:
            case Key.Minus:
                return AdjustHighlightedField(-1);
            case Key.Right:
            case Key.Plus:
                return AdjustHighlightedField(1);
            case Key.Tab:
                ToggleStarterKitTooltip();
                return true;
            case Key.H:
                HelpRequested?.Invoke();
                return true;
            case Key.T:
                DevToolsRequested?.Invoke();
                return true;
            default:
                return false;
        }
    }

    protected override void ActivateSelected()
    {
        switch (ResolveSelectedAction())
        {
            case MenuAction.Start:
                if (_gameManager is null)
                {
                    UpdateStatus("Start unavailable: GameManager autoload missing.");
                    return;
                }

                if (!IsArchetypeUnlocked(Archetypes[_archetypeIndex]))
                {
                    UpdateStatus($"{Archetypes[_archetypeIndex].DisplayName} is locked. Open Meta Shop to unlock it.");
                    return;
                }

                _gameManager?.SetCharacterCreationOptions(BuildCharacterCreationOptions());
                UpdateStatus($"Deploying seed {PendingSeed}...");
                _gameManager?.StartNewGame(PendingSeed);
                if (_gameManager?.CurrentState == GameManager.GameState.Playing)
                {
                    Close();
                    GameStarted?.Invoke();
                }
                break;
            case MenuAction.Name:
                CycleName(1);
                break;
            case MenuAction.Archetype:
                CycleArchetype(1);
                break;
            case MenuAction.Origin:
                CycleOrigin(1);
                break;
            case MenuAction.Trait:
                CycleTrait(1);
                break;
            case MenuAction.Race:
                CycleRace(1);
                break;
            case MenuAction.Gender:
                CycleGender(1);
                break;
            case MenuAction.Appearance:
                CycleAppearance(1);
                break;
            case MenuAction.Vitality:
            case MenuAction.Power:
            case MenuAction.Guard:
            case MenuAction.Finesse:
                AdjustAllocation(ResolveSelectedAction(), 1);
                break;
            case MenuAction.Seed:
                BeginSeedEdit();
                break;
            case MenuAction.DevTools:
                DevToolsRequested?.Invoke();
                break;
            case MenuAction.Help:
                HelpRequested?.Invoke();
                break;
            case MenuAction.LoadSlot1:
                _eventBus?.EmitLoadRequested(1);
                break;
            case MenuAction.LoadSlot2:
                _eventBus?.EmitLoadRequested(2);
                break;
            case MenuAction.LoadSlot3:
                _eventBus?.EmitLoadRequested(3);
                break;
            case MenuAction.DailyChallenge:
                StartDailyChallenge();
                break;
            case MenuAction.MetaShop:
                MetaShopRequested?.Invoke();
                break;
            case MenuAction.Quit:
                GetTree().Quit();
                break;
        }
    }

    protected override void Cancel()
    {
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            UpdateStatus("Save loaded.");
            Close();
        }
    }

    private void OnLogMessage(string message, LogCategory category)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (message.StartsWith("Failed to start a new game: ", System.StringComparison.Ordinal))
        {
            UpdateStatus($"Start failed: {message[28..]}");
            return;
        }

        if (message.StartsWith("Runtime initialization failed: ", System.StringComparison.Ordinal))
        {
            UpdateStatus($"Runtime init failed: {message[31..]}");
            return;
        }

        if (message.StartsWith("Cannot start a new game", System.StringComparison.Ordinal)
            || message.StartsWith("Load failed", System.StringComparison.Ordinal))
        {
            UpdateStatus(message);
        }
    }

    private void UpdateStatus(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? "Ready for deployment." : message.Trim();
        if (normalized.Length > 72)
        {
            normalized = normalized[..69] + "...";
        }

        if (_statusMessage == normalized)
        {
            return;
        }

        _statusMessage = normalized;
        RebuildOptions();
    }

    private bool AdjustHighlightedField(int delta)
    {
        switch (ResolveSelectedAction())
        {
            case MenuAction.Name:
                CycleName(delta);
                return true;
            case MenuAction.Archetype:
                CycleArchetype(delta);
                return true;
            case MenuAction.Origin:
                CycleOrigin(delta);
                return true;
            case MenuAction.Trait:
                CycleTrait(delta);
                return true;
            case MenuAction.Race:
                CycleRace(delta);
                return true;
            case MenuAction.Gender:
                CycleGender(delta);
                return true;
            case MenuAction.Appearance:
                CycleAppearance(delta);
                return true;
            case MenuAction.Vitality:
            case MenuAction.Power:
            case MenuAction.Guard:
            case MenuAction.Finesse:
                return AdjustAllocation(ResolveSelectedAction(), delta);
            case MenuAction.Seed:
                return false;
            default:
                return false;
        }
    }

    private void CycleName(int delta)
    {
        _nameIndex = WrapIndex(_nameIndex + delta, NameOptions.Length);
        RebuildOptions();
    }

    private void CycleArchetype(int delta)
    {
        _archetypeIndex = WrapIndex(_archetypeIndex + delta, Archetypes.Length);
        RebuildOptions();
    }

    private void CycleOrigin(int delta)
    {
        _originIndex = WrapIndex(_originIndex + delta, Origins.Length);
        RebuildOptions();
    }

    private void CycleTrait(int delta)
    {
        _traitIndex = WrapIndex(_traitIndex + delta, Traits.Length);
        RebuildOptions();
    }

    private void CycleRace(int delta)
    {
        _raceIndex = WrapIndex(_raceIndex + delta, RaceOptions.Length);
        RebuildOptions();
    }

    private void CycleGender(int delta)
    {
        _genderIndex = WrapIndex(_genderIndex + delta, GenderOptions.Length);
        RebuildOptions();
    }

    private void CycleAppearance(int delta)
    {
        _appearanceIndex = WrapIndex(_appearanceIndex + delta, AppearanceOptions.Length);
        RebuildOptions();
    }

    private bool AdjustAllocation(MenuAction selectedAction, int delta)
    {
        return selectedAction switch
        {
            MenuAction.Vitality => AdjustAllocation(ref _vitalityPoints, delta),
            MenuAction.Power => AdjustAllocation(ref _powerPoints, delta),
            MenuAction.Guard => AdjustAllocation(ref _guardPoints, delta),
            MenuAction.Finesse => AdjustAllocation(ref _finessePoints, delta),
            _ => false,
        };
    }

    private bool AdjustAllocation(ref int field, int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        var applied = false;
        while (delta > 0)
        {
            if (RemainingPoints <= 0)
            {
                break;
            }

            field++;
            delta--;
            applied = true;
        }

        while (delta < 0)
        {
            if (field <= 0)
            {
                break;
            }

            field--;
            delta++;
            applied = true;
        }

        if (applied)
        {
            RebuildOptions();
        }

        return applied;
    }

    private void RebuildOptions()
    {
        var selectedAction = ResolveSelectedAction();
        if (selectedAction == MenuAction.None)
        {
            selectedAction = MenuAction.Start;
        }

        _optionActions.Clear();
        ConfigureOptions();
        AddSection("EXPEDITION");
        AddOption("Start Game", MenuAction.Start);
        AddOption($"Name: {NameOptions[_nameIndex]}", MenuAction.Name);
        AddSection("BUILD");
        AddOption($"Archetype: {FormatArchetypeName(Archetypes[_archetypeIndex])}", MenuAction.Archetype);
        AddOption($"Origin: {Origins[_originIndex].DisplayName}", MenuAction.Origin);
        AddOption($"Trait: {Traits[_traitIndex].DisplayName}", MenuAction.Trait);
        AddSection("IDENTITY");
        AddOption($"Race: {RaceOptions[_raceIndex]}", MenuAction.Race);
        AddOption($"Gender: {GenderOptions[_genderIndex]}", MenuAction.Gender);
        AddOption($"Appearance: {AppearanceOptions[_appearanceIndex]}", MenuAction.Appearance);
        AddSection("TRAINING");
        AddOption($"Vitality (+3 Max HP): {_vitalityPoints}", MenuAction.Vitality);
        AddOption($"Power (+1 Attack): {_powerPoints}", MenuAction.Power);
        AddOption($"Guard (+1 Defense): {_guardPoints}", MenuAction.Guard);
        AddOption($"Finesse (+1 Accuracy/Evasion): {_finessePoints}", MenuAction.Finesse);
        AddSection("SYSTEM");
        AddOption(FormatSeedOption(), MenuAction.Seed);
        AddOption("Load Slot 1", MenuAction.LoadSlot1);
        AddOption("Load Slot 2", MenuAction.LoadSlot2);
        AddOption("Load Slot 3", MenuAction.LoadSlot3);
        AddOption(FormatDailyChallengeOption(), MenuAction.DailyChallenge);
        AddOption("Meta Shop", MenuAction.MetaShop);
        AddOption("Dev Tools", MenuAction.DevTools);
        AddOption("Quit", MenuAction.Quit);

        var restoredIndex = _optionActions.IndexOf(selectedAction);
        if (restoredIndex >= 0)
        {
            SetSelection(restoredIndex);
        }

        RefreshStarterKitTooltip();
    }

    private string FormatSeedOption()
    {
        return _isEditingSeed ? $"Seed: {_seedEditBuffer}_ (Enter to save, Esc to cancel)" : $"Seed: {PendingSeed}";
    }

    private string FormatDailyChallengeOption()
    {
        var date = DailySeedGenerator.GetTodaysDateString();
        var seed = DailySeedGenerator.GetTodaysSeed();
        var completed = _dailyChallenge?.TodayCompleted == true ? " COMPLETE" : string.Empty;
        return $"Daily Challenge: {date} #{seed:X8}{completed}";
    }

    private string BuildDailySummaryLine()
    {
        var seed = DailySeedGenerator.GetTodaysSeed();
        var best = _dailyChallenge?.TodayBestScore ?? 0;
        var status = _dailyChallenge?.TodayCompleted == true ? "complete" : "open";
        return $"Daily     {DailySeedGenerator.GetTodaysDateString()}  Seed #{seed:X8}  Best {best} ({status})";
    }

    private void StartDailyChallenge()
    {
        if (_gameManager is null)
        {
            UpdateStatus("Daily unavailable: GameManager autoload missing.");
            return;
        }

        if (_dailyChallenge?.TodayCompleted == true)
        {
            UpdateStatus("Today's daily challenge is already completed.");
            return;
        }

        if (!IsArchetypeUnlocked(Archetypes[_archetypeIndex]))
        {
            UpdateStatus($"{Archetypes[_archetypeIndex].DisplayName} is locked. Open Meta Shop to unlock it.");
            return;
        }

        PendingSeed = DailySeedGenerator.GetTodaysSeed();
        _seedEditBuffer = PendingSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _gameManager.SetCharacterCreationOptions(BuildCharacterCreationOptions());
        UpdateStatus($"Starting daily seed #{PendingSeed:X8}...");
        _gameManager.StartNewGame(PendingSeed);
        if (_gameManager.CurrentState == GameManager.GameState.Playing)
        {
            Close();
            GameStarted?.Invoke();
        }
    }

    private void BeginSeedEdit()
    {
        _seedEditBuffer = string.Empty;
        _isEditingSeed = true;
        UpdateStatus("Type a seed, then press Enter to save or Esc to cancel.");
    }

    private bool HandleSeedEditKey(Key key)
    {
        if (key is Key.Enter or Key.KpEnter)
        {
            CommitSeedEdit();
            return true;
        }

        if (key is Key.Escape)
        {
            _isEditingSeed = false;
            _seedEditBuffer = PendingSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UpdateStatus("Seed edit canceled.");
            RebuildOptions();
            return true;
        }

        var keyName = key.ToString();
        if (keyName is "Backspace")
        {
            if (_seedEditBuffer.Length > 0)
            {
                _seedEditBuffer = _seedEditBuffer[..^1];
                RebuildOptions();
            }

            return true;
        }

        if (keyName is "Delete")
        {
            _seedEditBuffer = string.Empty;
            RebuildOptions();
            return true;
        }

        var digit = TryGetSeedDigit(keyName);
        if (digit is null)
        {
            return true;
        }

        if (_seedEditBuffer.Length < 10)
        {
            _seedEditBuffer += digit.Value;
            RebuildOptions();
        }

        return true;
    }

    private void CommitSeedEdit()
    {
        if (!int.TryParse(_seedEditBuffer, out var seed) || seed <= 0)
        {
            seed = 1;
        }

        PendingSeed = seed;
        _seedEditBuffer = PendingSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _isEditingSeed = false;
        UpdateStatus($"Seed set to {PendingSeed}.");
        RebuildOptions();
    }

    private static char? TryGetSeedDigit(string keyName)
    {
        return keyName switch
        {
            "Zero" or "Key0" or "Kp0" or "Num0" => '0',
            "One" or "Key1" or "Kp1" or "Num1" => '1',
            "Two" or "Key2" or "Kp2" or "Num2" => '2',
            "Three" or "Key3" or "Kp3" or "Num3" => '3',
            "Four" or "Key4" or "Kp4" or "Num4" => '4',
            "Five" or "Key5" or "Kp5" or "Num5" => '5',
            "Six" or "Key6" or "Kp6" or "Num6" => '6',
            "Seven" or "Key7" or "Kp7" or "Num7" => '7',
            "Eight" or "Key8" or "Kp8" or "Num8" => '8',
            "Nine" or "Key9" or "Kp9" or "Num9" => '9',
            _ => null,
        };
    }

    private void ToggleStarterKitTooltip()
    {
        if (_starterKitTooltipVisible)
        {
            HideStarterKitTooltip();
            return;
        }

        _starterKitTooltipVisible = true;
        RefreshStarterKitTooltip();
    }

    private void RefreshStarterKitTooltip()
    {
        if (!_starterKitTooltipVisible || !Visible)
        {
            return;
        }

        var tooltip = ResolveStarterKitTooltip();
        var body = string.Join(
            "\n",
            "Equipped items start worn. Pack items are carried supplies.",
            string.Empty,
            BuildStarterKitPreviewText());
        tooltip.ShowShortcutTooltip("Starter Kit", body, tooltip.ResolveBottomRightPosition(GetViewportRect().Size));
    }

    private Tooltip ResolveStarterKitTooltip()
    {
        if (_starterKitTooltip is not null)
        {
            return _starterKitTooltip;
        }

        _starterKitTooltip = new Tooltip { ZIndex = ZIndex + 5 };
        AddChild(_starterKitTooltip);
        _starterKitTooltip._Ready();
        return _starterKitTooltip;
    }

    private void HideStarterKitTooltip()
    {
        _starterKitTooltipVisible = false;
        _starterKitTooltip?.Hide();
    }

    private void AddSection(string title)
    {
        _optionActions.Add(MenuAction.None);
        ConfigureSectionHeader(title);
    }

    private void AddOption(string label, MenuAction action)
    {
        _optionActions.Add(action);
        ConfigureOption(label);
    }

    private MenuAction ResolveSelectedAction()
    {
        return SelectedIndex >= 0 && SelectedIndex < _optionActions.Count ? _optionActions[SelectedIndex] : MenuAction.None;
    }

    private PlayerVisualProfile ResolveCurrentProfile()
    {
        return PlayerVisualCatalog.Resolve(
            RaceOptions[_raceIndex].ToLowerInvariant(),
            GenderOptions[_genderIndex].ToLowerInvariant(),
            AppearanceOptions[_appearanceIndex].ToLowerInvariant(),
            archetypeId: Archetypes[_archetypeIndex].ArchetypeId);
    }

    private void EnsurePreviewVisuals(Panel panel)
    {
        if (_previewPanel is not null)
        {
            return;
        }

        _previewPanel = new Panel
        {
            Name = "PreviewPanel",
        };
        _previewFrame = new ColorRect
        {
            Name = "PreviewFrame",
            Color = UiStyle.PanelInner(),
        };
        _previewBody = new TextureRect
        {
            Name = "PreviewBody",
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = PlayerVisualCatalog.GetBaseTexture(ResolveCurrentProfile()),
        };
        _previewAccentBand = new ColorRect
        {
            Name = "PreviewAccentBand",
        };
        _previewSigil = new Label
        {
            Name = "PreviewSigil",
        };
        _previewDetail = new Label
        {
            Name = "PreviewDetail",
        };
        _previewKitLabel = new Label
        {
            Name = "PreviewKitLabel",
        };
        _previewStatsLabel = new Label
        {
            Name = "PreviewStatsLabel",
        };
        _previewTitle = new Label
        {
            Name = "PreviewTitle",
        };
        _previewSubtitle = new Label
        {
            Name = "PreviewSubtitle",
        };
        _previewVariantId = new Label
        {
            Name = "PreviewVariantId",
        };

        _previewPanel.AddChild(_previewFrame);
        _previewPanel.AddChild(_previewBody);
        _previewPanel.AddChild(_previewAccentBand);
        _previewPanel.AddChild(_previewSigil);
        _previewPanel.AddChild(_previewDetail);
        _previewPanel.AddChild(_previewKitLabel);
        _previewPanel.AddChild(_previewStatsLabel);
        _previewPanel.AddChild(_previewTitle);
        _previewPanel.AddChild(_previewSubtitle);
        _previewPanel.AddChild(_previewVariantId);
        panel.AddChild(_previewPanel);
    }

    private void LayoutPreview(Vector2 previewSize)
    {
        if (_previewFrame is null
            || _previewBody is null
            || _previewAccentBand is null
            || _previewSigil is null
            || _previewDetail is null
            || _previewKitLabel is null
            || _previewStatsLabel is null
            || _previewTitle is null
            || _previewSubtitle is null
            || _previewVariantId is null)
        {
            return;
        }

        var inset = 16f;
        var frameWidth = System.Math.Max(90f, previewSize.X - (inset * 2f));
        var frameHeight = System.Math.Clamp(previewSize.Y * 0.32f, PreviewFrameMinimumHeight, 180f);
        var titleTop = inset + frameHeight + 26f;
        var kitTop = titleTop + 52f;
        var remaining = System.Math.Max(90f, previewSize.Y - kitTop - inset);
        var kitHeight = remaining * 0.30f;
        var statsHeight = remaining * 0.30f;
        var pathHeight = remaining * 0.18f;
        var variantTop = kitTop + kitHeight + statsHeight + pathHeight + 20f;

        _previewFrame.Position = new Vector2(inset, inset);
        _previewFrame.Size = new Vector2(frameWidth, frameHeight);

        _previewBody.Position = new Vector2(inset + 10f, inset + 10f);
        _previewBody.Size = new Vector2(System.Math.Max(60f, frameWidth - 20f), System.Math.Max(80f, frameHeight - 20f));

        _previewAccentBand.Position = new Vector2(inset, inset + frameHeight + 8f);
        _previewAccentBand.Size = new Vector2(frameWidth, 8f);

        _previewSigil.Position = new Vector2(inset + 8f, 10f);
        _previewSigil.Size = new Vector2(32f, 24f);

        _previewKitLabel.Position = new Vector2(inset, kitTop);
        _previewKitLabel.Size = new Vector2(frameWidth, System.Math.Max(36f, kitHeight - 4f));

        _previewStatsLabel.Position = new Vector2(inset, kitTop + kitHeight + 8f);
        _previewStatsLabel.Size = new Vector2(frameWidth, System.Math.Max(36f, statsHeight - 4f));

        _previewDetail.Position = new Vector2(inset, kitTop + kitHeight + statsHeight + 16f);
        _previewDetail.Size = new Vector2(frameWidth, System.Math.Max(28f, pathHeight));

        _previewTitle.Position = new Vector2(inset, titleTop);
        _previewTitle.Size = new Vector2(frameWidth, 24f);

        _previewSubtitle.Position = new Vector2(inset, titleTop + 24f);
        _previewSubtitle.Size = new Vector2(frameWidth, 24f);

        _previewVariantId.Position = new Vector2(inset, System.Math.Min(variantTop, previewSize.Y - inset - 34f));
        _previewVariantId.Size = new Vector2(frameWidth, System.Math.Max(32f, previewSize.Y - _previewVariantId.Position.Y - inset));
    }

    private void RefreshPreviewContent()
    {
        var profile = ResolveCurrentProfile();
        if (_previewBody is not null)
        {
            _previewBody.Texture = PlayerVisualCatalog.GetBaseTexture(profile);
            _previewBody.Modulate = profile.BodyTint;
        }

        if (_previewAccentBand is not null)
        {
            _previewAccentBand.Color = profile.AccentTint;
        }

        if (_previewSigil is not null)
        {
            _previewSigil.Text = profile.RaceSigil;
            _previewSigil.Modulate = profile.AccentTint;
        }

        if (_previewKitLabel is not null)
        {
            _previewKitLabel.Text = ClampTextLines(WrapPreviewText(BuildStarterKitPreviewText(), _previewKitLabel.Size.X), ResolveLineCapacity(_previewKitLabel.Size.Y));
            _previewKitLabel.Modulate = UiStyle.Parchment();
        }

        if (_previewStatsLabel is not null)
        {
            _previewStatsLabel.Text = ClampTextLines(BuildPreviewStatsText(), ResolveLineCapacity(_previewStatsLabel.Size.Y));
            _previewStatsLabel.Modulate = UiStyle.Parchment();
        }

        if (_previewDetail is not null)
        {
            _previewDetail.Text = ClampTextLines(WrapPreviewText(BuildPreviewPathText(), _previewDetail.Size.X), ResolveLineCapacity(_previewDetail.Size.Y));
            _previewDetail.Modulate = UiStyle.MutedText();
        }

        if (_previewTitle is not null)
        {
            _previewTitle.Text = FitLabelText(profile.Title.ToUpperInvariant(), _previewTitle.Size.X);
            _previewTitle.Modulate = UiStyle.BrightGold();
        }

        if (_previewSubtitle is not null)
        {
            _previewSubtitle.Text = FitLabelText($"{profile.Subtitle}  {profile.AppearanceMark}", _previewSubtitle.Size.X);
            _previewSubtitle.Modulate = profile.AccentTint;
        }

        if (_previewVariantId is not null)
        {
            _previewVariantId.Text = ClampTextLines($"Variant ID\n{profile.VariantId}", ResolveLineCapacity(_previewVariantId.Size.Y));
            _previewVariantId.Modulate = UiStyle.MutedText();
        }
    }

    private static int ResolveLineCapacity(float height)
    {
        return System.Math.Max(1, (int)System.Math.Floor(height / 16f));
    }

    private static string ClampTextLines(string text, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
        {
            return string.Empty;
        }

        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
        {
            return text;
        }

        var visible = lines.Take(System.Math.Max(1, maxLines)).ToArray();
        visible[^1] = "...";
        return string.Join("\n", visible);
    }

    private GameManager.CharacterCreationOptions BuildCharacterCreationOptions()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        var items = new List<string>();
        items.AddRange(archetype.StartingItems);
        items.AddRange(origin.StartingItems);
        items.AddRange(trait.StartingItems);

        return new GameManager.CharacterCreationOptions(
            NameOptions[_nameIndex],
            archetype.ArchetypeId,
            origin.DisplayName,
            trait.DisplayName,
            archetype.Bonus.MaxHp + origin.Bonus.MaxHp + trait.Bonus.MaxHp + (_vitalityPoints * 3),
            archetype.Bonus.Attack + origin.Bonus.Attack + trait.Bonus.Attack + _powerPoints,
            archetype.Bonus.Defense + origin.Bonus.Defense + trait.Bonus.Defense + _guardPoints,
            archetype.Bonus.Accuracy + origin.Bonus.Accuracy + trait.Bonus.Accuracy + _finessePoints,
            archetype.Bonus.Evasion + origin.Bonus.Evasion + trait.Bonus.Evasion + _finessePoints,
            archetype.Bonus.Speed + origin.Bonus.Speed + trait.Bonus.Speed,
            archetype.Bonus.ViewRadius + origin.Bonus.ViewRadius + trait.Bonus.ViewRadius,
            archetype.Bonus.InventoryCapacity + origin.Bonus.InventoryCapacity + trait.Bonus.InventoryCapacity,
            items,
            archetype.EquippedItems,
            RaceOptions[_raceIndex].ToLowerInvariant(),
            GenderOptions[_genderIndex].ToLowerInvariant(),
            AppearanceOptions[_appearanceIndex].ToLowerInvariant());
    }

    private int RemainingPoints => AllocationBudget - (_vitalityPoints + _powerPoints + _guardPoints + _finessePoints);

    private bool IsArchetypeUnlocked(ArchetypeOption archetype)
    {
        if (archetype.ArchetypeId == "vanguard" || _metaProgression is null)
        {
            return true;
        }

        return _metaProgression.IsUnlocked($"unlock_{archetype.ArchetypeId}");
    }

    private string FormatArchetypeName(ArchetypeOption archetype)
    {
        if (IsArchetypeUnlocked(archetype))
        {
            return archetype.DisplayName;
        }

        var upgradeId = $"unlock_{archetype.ArchetypeId}";
        var unlock = _metaProgression?.Upgrades.FirstOrDefault(upgrade => upgrade.Id == upgradeId);
        var cost = unlock?.GetCostForLevel(_metaProgression?.GetUnlockLevel(upgradeId) ?? 0) ?? 0;
        return cost > 0 ? $"{archetype.DisplayName} [LOCKED {cost} Echoes]" : $"{archetype.DisplayName} [LOCKED]";
    }

    private static int WrapIndex(int index, int count)
    {
        return (index % count + count) % count;
    }
}
