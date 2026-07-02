using System.Linq;
using Godot;

namespace Godotussy;

public partial class MetaShopUI : MenuBase
{
    private MetaProgressionManager? _metaProgression;

    public MetaShopUI()
    {
        Name = "MetaShopUI";
        Title = "Echo Workshop";
        Visible = false;
    }

    public void Bind(MetaProgressionManager? metaProgression)
    {
        _metaProgression = metaProgression;
        RefreshOptions();
    }

    public override void Open()
    {
        RefreshOptions();
        base.Open();
    }

    public string BuildShopMarkup() => BuildBodyText();

    protected override string BuildBodyText()
    {
        if (_metaProgression is null)
        {
            return "Meta progression is unavailable.";
        }

        var lines = _metaProgression.Upgrades.Select(upgrade =>
        {
            var level = _metaProgression.GetUnlockLevel(upgrade.Id);
            var maxed = level >= upgrade.MaxLevel;
            var cost = maxed ? 0 : upgrade.GetCostForLevel(level);
            var lockedMarker = upgrade.Effect == "unlock_archetype" && level == 0 ? " [LOCKED ARCHETYPE]" : string.Empty;
            var costText = maxed ? "MAX" : $"{cost} Echoes";
            return $"[b]{ItemRarityPresentation.EscapeBBCode(upgrade.DisplayName)}[/b]{lockedMarker}\nLevel {level}/{upgrade.MaxLevel}  Cost: {costText}\n{ItemRarityPresentation.EscapeBBCode(upgrade.Description)}";
        });

        var ascension = _metaProgression.HasCompletedFirstClear
            ? $"Ascension: {_metaProgression.AscensionLevel}/10\nHigher levels enable authored challenge modifiers, including stronger shop prices."
            : "Ascension: locked until first clear.";
        return $"Echoes: {_metaProgression.GetEchoes()}\n{ascension}\n\n" + string.Join("\n\n", lines);
    }

    protected override string BuildFooterText() => "Enter buy/adjust  Ascension warns before harder runs  Esc return";

    protected override void ActivateSelected()
    {
        if (_metaProgression is null)
        {
            Close();
            return;
        }

        if (SelectedIndex == ResolveBackOptionIndex())
        {
            Close();
            return;
        }

        if (SelectedIndex == ResolveAscensionDownOptionIndex())
        {
            AdjustAscension(-1);
            return;
        }

        if (SelectedIndex == ResolveAscensionUpOptionIndex())
        {
            AdjustAscension(1);
            return;
        }

        if (_metaProgression is null || SelectedIndex < 0 || SelectedIndex >= _metaProgression.Upgrades.Count)
        {
            return;
        }

        _metaProgression.TryUpgrade(_metaProgression.Upgrades[SelectedIndex].Id);
        RefreshOptions();
        SetSelection(SelectedIndex);
    }

    private void RefreshOptions()
    {
        if (_metaProgression is null)
        {
            ConfigureOptions("Back to Title");
            return;
        }

        ConfigureOptions(_metaProgression.Upgrades.Select(upgrade =>
        {
            var level = _metaProgression.GetUnlockLevel(upgrade.Id);
            var cost = level >= upgrade.MaxLevel ? "MAX" : $"{upgrade.GetCostForLevel(level)} Echoes";
            return $"{upgrade.DisplayName} ({level}/{upgrade.MaxLevel}) {cost}";
        }).Concat(new[]
        {
            $"Ascension - ({_metaProgression.AscensionLevel}/10)",
            _metaProgression.HasCompletedFirstClear ? $"Ascension + ({_metaProgression.AscensionLevel}/10)" : "Ascension + (locked)",
            "Back to Title",
        }).ToArray());
    }

    private int ResolveAscensionDownOptionIndex() => _metaProgression?.Upgrades.Count ?? 0;

    private int ResolveAscensionUpOptionIndex() => (_metaProgression?.Upgrades.Count ?? 0) + 1;

    private int ResolveBackOptionIndex() => (_metaProgression?.Upgrades.Count ?? 0) + 2;

    private void AdjustAscension(int delta)
    {
        if (_metaProgression is null)
        {
            return;
        }

        _metaProgression.SetAscensionLevel(_metaProgression.AscensionLevel + delta);
        RefreshOptions();
        SetSelection(delta > 0 ? ResolveAscensionUpOptionIndex() : ResolveAscensionDownOptionIndex());
    }
}
