using System.Collections.Generic;
using Godot;

namespace Godotussy;

public partial class MainMenu : MenuBase
{
    private const float PreviewPadding = 20f;
    private const float PreviewFrameHeight = 150f;

    private sealed record ArchetypeOption(
        string DisplayName,
        string Summary,
        int BonusMaxHp,
        int BonusAttack,
        int BonusDefense,
        int BonusEvasion,
        int BonusSpeed,
        int BonusViewRadius,
        IReadOnlyList<string> StartingItems,
        IReadOnlyList<string> EquippedItems);

    private sealed record OriginOption(
        string DisplayName,
        string Summary,
        int BonusMaxHp,
        int BonusAttack,
        int BonusDefense,
        int BonusEvasion,
        int BonusSpeed,
        int BonusViewRadius,
        int InventoryCapacityBonus,
        IReadOnlyList<string> StartingItems);

    private sealed record TraitOption(
        string DisplayName,
        string Summary,
        int BonusMaxHp,
        int BonusAttack,
        int BonusDefense,
        int BonusAccuracy,
        int BonusEvasion,
        int BonusSpeed,
        int BonusViewRadius,
        int InventoryCapacityBonus,
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
            "A tough delver who starts armed and ready for the front line.",
            8,
            2,
            1,
            0,
            -5,
            0,
            new[] { "sword_iron", "shield_wooden", "potion_health", "potion_health" },
            new[] { "sword_iron", "shield_wooden" }),
        new(
            "Skirmisher",
            "A fast striker who trades durability for initiative and accuracy.",
            0,
            1,
            0,
            4,
            10,
            0,
            new[] { "dagger_venom", "potion_haste", "potion_health" },
            new[] { "dagger_venom" }),
        new(
            "Mystic",
            "A fragile explorer who leans on scrolls, vision, and mobility.",
            -4,
            0,
            -1,
            0,
            5,
            1,
            new[] { "scroll_fireball", "scroll_blink", "potion_health" },
            System.Array.Empty<string>()),
    };

    private static readonly OriginOption[] Origins =
    {
        new(
            "Survivor",
            "Veteran of too many bad runs. Starts a little tougher.",
            4,
            0,
            1,
            0,
            0,
            0,
            0,
            new[] { "potion_health" }),
        new(
            "Scout",
            "Maps routes quickly and sees danger earlier.",
            0,
            0,
            0,
            0,
            5,
            1,
            0,
            new[] { "potion_haste" }),
        new(
            "Scavenger",
            "Carries more, wastes less, and hoards every edge.",
            0,
            0,
            0,
            0,
            0,
            0,
            4,
            new[] { "potion_health", "scroll_blink" }),
    };

    private static readonly TraitOption[] Traits =
    {
        new(
            "Iron Will",
            "Push through attrition with a deeper life pool and sturdier posture.",
            4,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            new[] { "potion_health" }),
        new(
            "Quartermaster",
            "Travel heavier and keep extra supplies close at hand.",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            new[] { "potion_health", "potion_health" }),
        new(
            "Pathfinder",
            "See farther, move quicker, and carry a panic-scroll.",
            0,
            0,
            0,
            0,
            0,
            5,
            1,
            0,
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
    private Label? _previewTitle;
    private Label? _previewSubtitle;
    private Label? _previewVariantId;

    public int PendingSeed { get; private set; } = 1337;

    public event System.Action? GameStarted;

    public event System.Action? HelpRequested;

    public event System.Action? DevToolsRequested;

    public MainMenu()
    {
        Name = "MainMenu";
        Title = "GODOTUSSY ROGUELIKE";
        RebuildOptions();
        Visible = true;
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted += OnLoadCompleted;
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
        RebuildOptions();
    }

    protected override string BuildBodyText()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];
        return string.Join(
            "\n",
            $"Candidate: {NameOptions[_nameIndex]}",
            $"Archetype: {archetype.DisplayName}",
            $"Origin: {origin.DisplayName}",
            $"Trait: {trait.DisplayName}",
            $"Race: {RaceOptions[_raceIndex]}",
            $"Gender: {GenderOptions[_genderIndex]}",
            $"Appearance: {AppearanceOptions[_appearanceIndex]}",
            $"Training: VIT {_vitalityPoints}  POW {_powerPoints}  GRD {_guardPoints}  FIN {_finessePoints}",
            $"Points Remaining: {RemainingPoints}",
            string.Empty,
            BuildIdentityPreview(),
            string.Empty,
            archetype.Summary,
            origin.Summary,
            trait.Summary,
            string.Empty,
            BuildStatPreview(),
            string.Empty,
                "Use Left/Right or +/- to edit the highlighted field.",
                "Training raises max HP, attack, defense, and finesse.");
    }

    public string BuildStatPreview()
    {
        var archetype = Archetypes[_archetypeIndex];
        var origin = Origins[_originIndex];
        var trait = Traits[_traitIndex];

        var hp = BaseMaxHp + archetype.BonusMaxHp + origin.BonusMaxHp + trait.BonusMaxHp + (_vitalityPoints * 3);
        var atk = BaseAttack + archetype.BonusAttack + origin.BonusAttack + trait.BonusAttack + _powerPoints;
        var def = BaseDefense + archetype.BonusDefense + origin.BonusDefense + trait.BonusDefense + _guardPoints;
        var acc = BaseAccuracy + trait.BonusAccuracy + _finessePoints;
        var eva = BaseEvasion + archetype.BonusEvasion + origin.BonusEvasion + trait.BonusEvasion + _finessePoints;
        var spd = BaseSpeed + archetype.BonusSpeed + origin.BonusSpeed + trait.BonusSpeed;
        var vr = BaseViewRadius + archetype.BonusViewRadius + origin.BonusViewRadius + trait.BonusViewRadius;

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

    protected override Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var baseSize = base.ResolveDesiredPanelSize(viewportSize);
        return new Vector2(System.Math.Max(baseSize.X, 760f), System.Math.Max(baseSize.Y, 420f));
    }

    protected override void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
        EnsurePreviewVisuals(panel);

        var previewWidth = System.Math.Clamp(panelSize.X * 0.30f, 170f, 220f);
        var contentWidth = System.Math.Max(120f, panelSize.X - previewWidth - (PreviewPadding * 3f));
        label.Position = new Vector2(PreviewPadding, PreviewPadding);
        label.Size = new Vector2(contentWidth, System.Math.Max(0f, panelSize.Y - (PreviewPadding * 2f)));

        if (_previewPanel is null)
        {
            return;
        }

        _previewPanel.Visible = Visible;
        _previewPanel.Position = new Vector2(label.Position.X + label.Size.X + PreviewPadding, PreviewPadding);
        _previewPanel.Size = new Vector2(previewWidth, System.Math.Max(0f, panelSize.Y - (PreviewPadding * 2f)));
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
        switch (SelectedIndex)
        {
            case StartIndex:
                _gameManager?.SetCharacterCreationOptions(BuildCharacterCreationOptions());
                _gameManager?.StartNewGame(PendingSeed);
                _eventBus?.EmitLogMessage($"Starting new game with seed {PendingSeed}.");
                Close();
                GameStarted?.Invoke();
                break;
            case NameIndex:
                CycleName(1);
                break;
            case ArchetypeIndex:
                CycleArchetype(1);
                break;
            case OriginIndex:
                CycleOrigin(1);
                break;
            case TraitIndex:
                CycleTrait(1);
                break;
            case RaceIndex:
                CycleRace(1);
                break;
            case GenderIndex:
                CycleGender(1);
                break;
            case AppearanceIndex:
                CycleAppearance(1);
                break;
            case VitalityIndex:
            case PowerIndex:
            case GuardIndex:
            case FinesseIndex:
                AdjustAllocation(SelectedIndex, 1);
                break;
            case SeedIndex:
                PendingSeed++;
                RebuildOptions();
                break;
            case DevToolsIndex:
                DevToolsRequested?.Invoke();
                break;
            case HelpIndex:
                HelpRequested?.Invoke();
                break;
            case LoadSlot1Index:
                _eventBus?.EmitLoadRequested(1);
                break;
            case LoadSlot2Index:
                _eventBus?.EmitLoadRequested(2);
                break;
            case LoadSlot3Index:
                _eventBus?.EmitLoadRequested(3);
                break;
            case QuitIndex:
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
            Close();
        }
    }

    private bool AdjustHighlightedField(int delta)
    {
        switch (SelectedIndex)
        {
            case NameIndex:
                CycleName(delta);
                return true;
            case ArchetypeIndex:
                CycleArchetype(delta);
                return true;
            case OriginIndex:
                CycleOrigin(delta);
                return true;
            case TraitIndex:
                CycleTrait(delta);
                return true;
            case RaceIndex:
                CycleRace(delta);
                return true;
            case GenderIndex:
                CycleGender(delta);
                return true;
            case AppearanceIndex:
                CycleAppearance(delta);
                return true;
            case VitalityIndex:
            case PowerIndex:
            case GuardIndex:
            case FinesseIndex:
                return AdjustAllocation(SelectedIndex, delta);
            case SeedIndex:
                PendingSeed = System.Math.Max(1, PendingSeed + delta);
                RebuildOptions();
                return true;
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

    private bool AdjustAllocation(int selectedIndex, int delta)
    {
        return selectedIndex switch
        {
            VitalityIndex => AdjustAllocation(ref _vitalityPoints, delta),
            PowerIndex => AdjustAllocation(ref _powerPoints, delta),
            GuardIndex => AdjustAllocation(ref _guardPoints, delta),
            FinesseIndex => AdjustAllocation(ref _finessePoints, delta),
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
        ConfigureOptions(
            "Start Expedition",
            $"Name: {NameOptions[_nameIndex]}",
            $"Archetype: {Archetypes[_archetypeIndex].DisplayName}",
            $"Origin: {Origins[_originIndex].DisplayName}",
            $"Trait: {Traits[_traitIndex].DisplayName}",
            $"Race: {RaceOptions[_raceIndex]}",
            $"Gender: {GenderOptions[_genderIndex]}",
            $"Appearance: {AppearanceOptions[_appearanceIndex]}",
            $"Vitality (+3 Max HP): {_vitalityPoints}",
            $"Power (+1 Attack): {_powerPoints}",
            $"Guard (+1 Defense): {_guardPoints}",
            $"Finesse (+1 Accuracy/Evasion): {_finessePoints}",
            $"Seed: {PendingSeed}",
            "Dev Tools",
            "Help",
            "Load Slot 1",
            "Load Slot 2",
            "Load Slot 3",
            "Quit");
    }

    private PlayerVisualProfile ResolveCurrentProfile()
    {
        return PlayerVisualCatalog.Resolve(
            RaceOptions[_raceIndex].ToLowerInvariant(),
            GenderOptions[_genderIndex].ToLowerInvariant(),
            AppearanceOptions[_appearanceIndex].ToLowerInvariant(),
            archetypeId: Archetypes[_archetypeIndex].DisplayName);
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
            Color = new Color(0.10f, 0.12f, 0.18f, 1f),
        };
        _previewBody = new TextureRect
        {
            Name = "PreviewBody",
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = PlayerVisualCatalog.GetBaseTexture(),
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
            || _previewTitle is null
            || _previewSubtitle is null
            || _previewVariantId is null)
        {
            return;
        }

        var frameWidth = System.Math.Max(90f, previewSize.X - 32f);
        _previewFrame.Position = new Vector2(16f, 16f);
        _previewFrame.Size = new Vector2(frameWidth, PreviewFrameHeight);

        _previewBody.Position = new Vector2(28f, 28f);
        _previewBody.Size = new Vector2(System.Math.Max(60f, frameWidth - 24f), PreviewFrameHeight - 32f);

        _previewAccentBand.Position = new Vector2(24f, PreviewFrameHeight + 2f);
        _previewAccentBand.Size = new Vector2(System.Math.Max(50f, frameWidth - 16f), 6f);

        _previewSigil.Position = new Vector2(20f, 12f);
        _previewSigil.Size = new Vector2(32f, 24f);

        _previewDetail.Position = new Vector2(frameWidth - 4f, PreviewFrameHeight - 12f);
        _previewDetail.Size = new Vector2(24f, 24f);

        _previewTitle.Position = new Vector2(16f, PreviewFrameHeight + 24f);
        _previewTitle.Size = new Vector2(frameWidth, 24f);

        _previewSubtitle.Position = new Vector2(16f, PreviewFrameHeight + 48f);
        _previewSubtitle.Size = new Vector2(frameWidth, 24f);

        _previewVariantId.Position = new Vector2(16f, PreviewFrameHeight + 78f);
        _previewVariantId.Size = new Vector2(frameWidth, System.Math.Max(32f, previewSize.Y - (PreviewFrameHeight + 94f)));
    }

    private void RefreshPreviewContent()
    {
        var profile = ResolveCurrentProfile();
        if (_previewBody is not null)
        {
            _previewBody.Texture = PlayerVisualCatalog.GetBaseTexture();
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

        if (_previewDetail is not null)
        {
            _previewDetail.Text = profile.AppearanceMark;
            _previewDetail.Modulate = profile.DetailTint;
        }

        if (_previewTitle is not null)
        {
            _previewTitle.Text = profile.Title;
        }

        if (_previewSubtitle is not null)
        {
            _previewSubtitle.Text = profile.Subtitle;
        }

        if (_previewVariantId is not null)
        {
            _previewVariantId.Text = $"Variant: {profile.VariantId}";
        }
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
            archetype.DisplayName,
            origin.DisplayName,
            trait.DisplayName,
            archetype.BonusMaxHp + origin.BonusMaxHp + trait.BonusMaxHp + (_vitalityPoints * 3),
            archetype.BonusAttack + origin.BonusAttack + trait.BonusAttack + _powerPoints,
            archetype.BonusDefense + origin.BonusDefense + trait.BonusDefense + _guardPoints,
            trait.BonusAccuracy + _finessePoints,
            archetype.BonusEvasion + origin.BonusEvasion + trait.BonusEvasion + _finessePoints,
            archetype.BonusSpeed + origin.BonusSpeed + trait.BonusSpeed,
            archetype.BonusViewRadius + origin.BonusViewRadius + trait.BonusViewRadius,
            origin.InventoryCapacityBonus + trait.InventoryCapacityBonus,
            items,
            archetype.EquippedItems,
            RaceOptions[_raceIndex].ToLowerInvariant(),
            GenderOptions[_genderIndex].ToLowerInvariant(),
            AppearanceOptions[_appearanceIndex].ToLowerInvariant());
    }

    private int RemainingPoints => AllocationBudget - (_vitalityPoints + _powerPoints + _guardPoints + _finessePoints);

    private static int WrapIndex(int index, int count)
    {
        return (index % count + count) % count;
    }
}