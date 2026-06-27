using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class LevelUpOverlay : Control
{
    private const float PanelWidth = 540f;
    private const float PanelHeight = 380f;
    private const float PanelPadding = 20f;
    private const float OuterMargin = 32f;
    private const float TopMargin = 40f;

    private GameManager? _gameManager;
    private Panel? _panel;
    private RichTextLabel? _label;
    private int _selectedIndex;

    public string SummaryText { get; private set; } = string.Empty;

    public LevelUpOverlay()
    {
        Name = "LevelUpOverlay";
        Visible = false;
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void Bind(GameManager? gameManager)
    {
        _gameManager = gameManager;
        Refresh();
    }

    public void Open()
    {
        Visible = true;
        _selectedIndex = 0;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        _selectedIndex = 0;
        RefreshVisualState();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        if (key == Key.Escape)
        {
            Close();
            return true;
        }

        var choices = _gameManager?.GetAvailablePerkChoices();
        if (choices is null || choices.Count == 0)
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
                _selectedIndex = (_selectedIndex - 1 + choices.Count) % choices.Count;
                Refresh();
                return true;
            case Key.Down:
                _selectedIndex = (_selectedIndex + 1) % choices.Count;
                Refresh();
                return true;
            case Key.Enter:
            case Key.KpEnter:
            case Key.Right:
                _gameManager?.TrySelectPerk(choices[_selectedIndex].TemplateId, out _);
                return true;
            default:
                return false;
        }
    }

    public void Refresh()
    {
        var choices = _gameManager?.GetAvailablePerkChoices();
        if (choices is not null && choices.Count > 0)
        {
            _selectedIndex = System.Math.Max(0, System.Math.Min(_selectedIndex, choices.Count - 1));
        }
        else
        {
            _selectedIndex = 0;
        }

        SummaryText = BuildBodyMarkup();
        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _label is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        ZIndex = 98;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _label = new RichTextLabel
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            BbcodeEnabled = true,
        };
        _panel.AddChild(_label);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _label is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);
        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = ResolvePanelPosition(viewportSize, panelSize);
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Position = new Vector2(PanelPadding, PanelPadding);
        _label.Size = new Vector2(
            System.Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
            System.Math.Max(0f, panelSize.Y - (PanelPadding * 2f)));
        _label.Clear();
        _label.AppendText(SummaryText);
    }

    private string BuildBodyMarkup()
    {
        var player = _gameManager?.World?.Player;
        var progression = player?.GetComponent<ProgressionComponent>();
        var choices = _gameManager?.GetAvailablePerkChoices();
        if (player is null || progression is null || choices is null || choices.Count == 0)
        {
            return string.Empty;
        }

        var selected = choices[_selectedIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.BrightGold())}]LEVEL UP[/color][/b]");
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.Parchment())}]{ItemRarityPresentation.EscapeBBCode($"{player.Name} can choose a new perk.")}[/color]");
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.WarningOrange())}]{ItemRarityPresentation.EscapeBBCode($"Pending choices: {progression.UnspentPerkChoices}")}[/color]");
        builder.AppendLine();
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.MutedText())}]Available Perks[/color][/b]");

        for (var index = 0; index < choices.Count; index++)
        {
            var marker = index == _selectedIndex ? ">>" : "  ";
            var emphasisOpen = index == _selectedIndex ? "[b]" : string.Empty;
            var emphasisClose = index == _selectedIndex ? "[/b]" : string.Empty;
            var color = index == _selectedIndex ? UiStyle.ToHex(UiStyle.BrightGold()) : UiStyle.ToHex(UiStyle.Parchment());
            builder.AppendLine($"{emphasisOpen}[color={color}]{ItemRarityPresentation.EscapeBBCode($"{marker} {choices[index].DisplayName}")}[/color]{emphasisClose} [i][color={UiStyle.ToHex(UiStyle.FaintText())}]{ItemRarityPresentation.EscapeBBCode($"(Lv {choices[index].UnlockLevel})")}[/color][/i]");
        }

        builder.AppendLine();
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.MutedText())}]Selected Perk[/color][/b]");
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.BrightGold())}]{ItemRarityPresentation.EscapeBBCode(selected.DisplayName)}[/color][/b]");
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.Parchment())}]{ItemRarityPresentation.EscapeBBCode(selected.Description)}[/color]");
        builder.AppendLine();
        builder.AppendLine($"[b][color={UiStyle.ToHex(UiStyle.MutedText())}]EFFECTS[/color][/b]");
        foreach (var effect in selected.Effects)
        {
            builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.ActiveGreen())}]{ItemRarityPresentation.EscapeBBCode($"+ {DescribeEffect(effect)}")}[/color]");
        }

        builder.AppendLine();
        builder.Append($"[i][color={UiStyle.ToHex(UiStyle.FaintText())}]{ItemRarityPresentation.EscapeBBCode("Up/Down: choose    Enter/Right: confirm    Esc close")}[/color][/i]");
        return builder.ToString().TrimEnd();
    }

    private static string DescribeEffect(PerkEffect effect)
    {
        return effect.Type switch
        {
            "stat_bonus" => $"{effect.Stat} {effect.Value:+#;-#;0}",
            "shop_discount_percent" => $"Merchant prices {effect.Value}% lower",
            _ => effect.Type,
        };
    }

    private static Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
    }

    private static Vector2 ResolvePanelPosition(Vector2 viewportSize, Vector2 panelSize)
    {
        var maxX = MathF.Max(0f, viewportSize.X - panelSize.X - OuterMargin);
        var preferredY = MathF.Max(TopMargin, viewportSize.Y * 0.12f);
        var maxY = MathF.Max(0f, viewportSize.Y - panelSize.Y - OuterMargin);
        return new Vector2(maxX, MathF.Min(preferredY, maxY));
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }
}
