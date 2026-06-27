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
            "START MENU / FOUNDRY",
            "Up/Down or W/S: move between entries.",
            "Left/Right or +/-: adjust the highlighted creation field.",
            "Enter: confirm or cycle the highlighted entry.  H: toggle help.",
            "",
            "BUILD AND IDENTITY",
            "Name: cosmetic identity for the run.  Seed: deterministic world generation.",
            "Archetype: starting combat style and loadout.  Origin: small secondary bonus.",
            "Trait: passive perk or bonus.  Training: spend 4 points across VIT/POW/GRD/FIN.",
            "Stat Preview: updates live as you change selections.",
            "",
            "RUN TOOLS",
            "Dev Tools opens the workshop.  Load Slot entries resume saved expeditions.");
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
            "CHARACTER GROWTH",
            "C toggles the sheet.  With points available: Up/Down selects a stat, Enter/Right/+ spends.",
            "Level Up choices open as a focused overlay when perk picks are waiting.",
            "",
            "INVENTORY / SHOPS",
            "Arrows move selection.  Enter/U/E uses, equips, or unequips.  D drops one from a stack.",
            "A toggles auto-equip upgrades.  Tab cycles sort and category grouping.",
            "Rarity is shown by color and text.  Equipment comparison appears automatically for gear.",
            "Aimed scrolls explain when targeting is required instead of silently consuming input.",
            "Shops use the same value and weight data.  In dialog or shops: F talks, Tab swaps buy/sell, Enter confirms.",
            "",
            "DEVELOPER WORKSHOP",
            "Tab switches rooms, items, enemies, and commands.  Up/Down selects a field.",
            "Left/Right or +/- adjusts the highlighted field.  Enter applies.  Esc or T closes.");
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
        var width = System.Math.Clamp(viewportSize.X * 0.60f, 560f, 800f);
        var height = System.Math.Min(viewportSize.Y * 0.80f, 620f);
        return new Vector2(width, height);
    }

    protected override float ResolveApproxLineHeight()
    {
        return 20f;
    }

    protected override void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
        panel.Modulate = UiStyle.GoldTrim();
        if (Backdrop is not null)
        {
            Backdrop.Color = UiStyle.PanelBlack(0.96f);
        }

        if (HeaderBand is not null)
        {
            HeaderBand.Color = UiStyle.PanelHighlight();
            HeaderBand.Size = new Vector2(panelSize.X, 46f);
        }

        if (TitleLabel is not null)
        {
            TitleLabel.Text = "HELP";
            TitleLabel.Modulate = UiStyle.BrightGold();
            TitleLabel.Position = new Vector2(20f, 12f);
            TitleLabel.Size = new Vector2(panelSize.X - 40f, 24f);
        }

        if (BodyCard is not null)
        {
            BodyCard.Color = UiStyle.PanelInner(0.98f);
        }

        label.Text = BuildReadableHelpText(_bodyText);
        label.Modulate = UiStyle.Parchment();

        if (OptionsCard is not null)
        {
            OptionsCard.Color = UiStyle.PanelHighlight();
            OptionsCard.Size = new Vector2(OptionsCard.Size.X, 34f);
        }

        if (FooterLabel is not null)
        {
            FooterLabel.Text = "[Up/Down] move    [Enter] confirm    [Esc] back";
            FooterLabel.Modulate = UiStyle.FaintText();
        }
    }

    private static string BuildReadableHelpText(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IsSectionHeader(line))
            {
                lines[i] = $"── {line} ─────────────────";
            }
        }

        return string.Join("\n", lines);
    }

    private static bool IsSectionHeader(string line)
    {
        return line == line.ToUpperInvariant()
            && line.Length > 0
            && !line.Contains(':', System.StringComparison.Ordinal);
    }
}
