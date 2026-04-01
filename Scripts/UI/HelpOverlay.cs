using Godot;

namespace Godotussy;

public partial class HelpOverlay : MenuBase
{
    private string _bodyText = string.Empty;

    public string CurrentBodyText => _bodyText;

    public HelpOverlay()
    {
        Name = "HelpOverlay";
        Title = "HELP";
        ConfigureOptions("Close");
        Visible = false;
    }

    public void OpenMainMenuHelp()
    {
        _bodyText = string.Join(
            "\n",
            "START MENU",
            "Up/Down or W/S: move between entries.",
            "Left/Right or +/-: adjust the highlighted creation field.",
            "Enter: confirm or cycle the highlighted entry.  H: toggle help.",
            "",
            "CHARACTER CREATION",
            "Name: cosmetic identity for the run.  Seed: deterministic world generation.",
            "Archetype: starting combat style and loadout.  Origin: small secondary bonus.",
            "Trait: passive perk or bonus.  Training: spend 4 points across VIT/POW/GRD/FIN.",
            "Stat Preview: updates live as you change selections.");
        base.Open();
    }

    public void OpenGameplayHelp()
    {
        _bodyText = string.Join(
            "\n",
            "FIELD CONTROLS",
            "Move: Arrows or W/A/S/D.  Wait: Space or .  Pick up: G.  Interact/talk: F.",
            "Stairs: Enter.  Inventory: I.  Character sheet: C.  Pause: Esc.  Help: H.",
            "Minimap: M or Tab.  Dev tools: T.  Debug overlay: Q.  Debug console: `.",
            "",
            "Character Sheet / Level Up",
            "C toggles the sheet.  With points available: Up/Down selects a stat, Enter/Right/+ spends.",
            "",
            "Developer Workshop",
            "Tab switches rooms, items, enemies, and commands.  Up/Down selects a field.",
            "Left/Right or +/- adjusts the highlighted field.  Enter applies.  Esc or T closes.",
            "",
            "Inventory / Shops",
            "Arrows move selection.  Enter/U/E uses, equips, or unequips.  D drops.  Tab cycles sort.",
            "Esc or I closes inventory.  Equipment comparison appears automatically for equippable items.",
            "Shops use the same value and weight data.  In dialog or shops: F talks, Tab swaps buy/sell, Enter confirms.");
        base.Open();
    }

    public bool ToggleForContext(bool mainMenuVisible)
    {
        if (Visible)
        {
            Close();
            return false;
        }

        if (mainMenuVisible)
        {
            OpenMainMenuHelp();
        }
        else
        {
            OpenGameplayHelp();
        }

        return true;
    }

    protected override string BuildBodyText() => _bodyText;

    protected override void ActivateSelected()
    {
        Close();
    }

    protected override bool HandleCustomKey(Key key)
    {
        if (key == Key.H)
        {
            Close();
            return true;
        }

        return false;
    }

    protected override Vector2 ResolveDesiredPanelSize(Vector2 viewportSize)
    {
        var maxWidth = viewportSize.X * 0.92f;
        var maxHeight = viewportSize.Y * 0.92f;
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(maxWidth, maxHeight), 24f);
    }

    protected override float ResolveApproxLineHeight()
    {
        return 20f;
    }
}