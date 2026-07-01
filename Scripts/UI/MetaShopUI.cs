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

        return $"Echoes: {_metaProgression.GetEchoes()}\n\n" + string.Join("\n\n", lines);
    }

    protected override string BuildFooterText() => "Enter buy upgrade  Esc return";

    protected override void ActivateSelected()
    {
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
            ConfigureOptions("Meta progression unavailable");
            return;
        }

        ConfigureOptions(_metaProgression.Upgrades.Select(upgrade =>
        {
            var level = _metaProgression.GetUnlockLevel(upgrade.Id);
            var cost = level >= upgrade.MaxLevel ? "MAX" : $"{upgrade.GetCostForLevel(level)} Echoes";
            return $"{upgrade.DisplayName} ({level}/{upgrade.MaxLevel}) {cost}";
        }).ToArray());
    }
}
