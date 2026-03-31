using Godot;

namespace Godotussy;

public partial class HelpOverlay : MenuBase
{
    private string _bodyText = string.Empty;

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
            "Start Menu",
            "",
            "Up/Down or W/S: move between menu entries",
            "Left/Right or +/-: adjust the highlighted creation field",
            "Enter: confirm or cycle the highlighted entry",
            "H: toggle this help panel",
            "",
            "Character Creation",
            "",
            "Name: cosmetic identity for the run",
            "Archetype: sets your starting combat style and loadout",
            "Origin: adds a small secondary bonus",
            "Seed: controls procedural generation deterministically");
        base.Open();
    }

    public void OpenGameplayHelp()
    {
        _bodyText = string.Join(
            "\n",
            "Field Controls",
            "",
            "Move: Arrow Keys or W/A/S/D",
            "Wait: Space or .",
            "Pick up item: G",
            "Use stairs: Enter",
            "Inventory: I",
            "Character sheet: C",
            "Help: H",
            "Dev tools: T or menu option",
            "Pause menu: Esc",
            "Toggle minimap: M or Tab",
            "Debug overlay: Q",
            "Debug console: `",
            "",
            "Developer Workshop",
            "",
            "Tab: switch between rooms, items, enemies, and commands",
            "Up/Down: move between workshop fields",
            "Left/Right or +/-: adjust the highlighted field",
            "Enter: apply the highlighted action",
            "Esc or T: close the workshop",
            "",
            "Inventory",
            "",
            "Arrows: move selection",
            "Enter/U/E: use, equip, or unequip",
            "D: drop item",
            "Tab: cycle sort mode",
            "Esc or I: close inventory");
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
}