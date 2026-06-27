using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class Tooltip : Control
{
    private const float PanelPadding = 10f;
    private Panel? _panel;
    private ColorRect? _background;
    private ColorRect? _borderTop;
    private ColorRect? _borderBottom;
    private ColorRect? _borderLeft;
    private ColorRect? _borderRight;
    private RichTextLabel? _titleLabel;
    private RichTextLabel? _bodyLabel;

    public string TitleText { get; private set; } = string.Empty;

    public string TitleMarkup { get; private set; } = string.Empty;

    public string BodyText { get; private set; } = string.Empty;

    public string BodyMarkup { get; private set; } = string.Empty;

    public Vector2 ScreenPosition { get; private set; }

    public Tooltip()
    {
        Name = "Tooltip";
        Visible = false;
        Size = new Vector2(280f, 220f);
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void ShowItemTooltip(ItemTemplate template, ItemInstance instance, Vector2 screenPos,
        string? comparisonText = null, bool equipped = false, string equippedSlot = "None")
    {
        TitleText = template.DisplayName;
        var rarityLabel = ItemRarityPresentation.ResolveDisplayLabel(template.Rarity);
        TitleMarkup = $"[b][color={UiStyle.ToHex(UiStyle.BrightGold())}]{ItemRarityPresentation.EscapeBBCode(template.DisplayName)}[/color][/b] "
            + $"[i][color={ItemRarityPresentation.ResolveHexColor(template.Rarity)}]{ItemRarityPresentation.EscapeBBCode(rarityLabel)}[/color][/i]";

        var lines = new List<string>
        {
            $"Rarity: {ItemRarityPresentation.ResolveDisplayLabel(template.Rarity)}",
            template.Description,
        };
        foreach (var modifier in template.StatModifiers)
        {
            lines.Add($"{modifier.Key}: {modifier.Value:+#;-#;0}");
        }

        if (template.MaxCharges > 1)
        {
            var remaining = instance.CurrentCharges > 0 ? instance.CurrentCharges : template.MaxCharges;
            lines.Add($"Charges: {remaining}/{template.MaxCharges}");
        }

        lines.Add($"Category: {template.Category}  ▪  Slot: {(template.Slot == EquipSlot.None ? "None" : template.Slot)}  ▪  Stack: {instance.StackCount}/{System.Math.Max(1, template.MaxStack)}");

        if (!string.IsNullOrEmpty(comparisonText))
        {
            lines.Add(comparisonText);
        }

        if (equipped)
        {
            lines.Add($"Equipped: {equippedSlot}");
        }

        lines.Add(equipped ? $"Status: Equipped in {equippedSlot}" : "Status: Carried");

        lines.Add(equipped ? "[E] Unequip  [D] Drop" : "[E] Equip/Use  [D] Drop");

        var visibleLines = ClampLines(lines, 11);
        BodyText = string.Join("\n", visibleLines);
        BodyMarkup = BuildItemBodyMarkup(template, visibleLines);
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
        RefreshVisualState();
    }

    public void ShowEnemyTooltip(IEntity enemy, Vector2 screenPos)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"HP: {enemy.Stats.HP}/{enemy.Stats.MaxHP}");
        builder.AppendLine($"ATK: {enemy.Stats.Attack}  DEF: {enemy.Stats.Defense}");

        var effects = StatusEffectProcessor.GetEffects(enemy);
        if (effects.Count > 0)
        {
            builder.AppendLine("Effects:");
            foreach (var effect in effects)
            {
                builder.AppendLine($"- {effect.Type} ({effect.RemainingTurns})");
            }
        }

        TitleText = enemy.Name;
        BodyText = builder.ToString().TrimEnd();
        BodyText = string.Join("\n", ClampLines(BodyText.Split('\n'), 9));
        TitleMarkup = ItemRarityPresentation.EscapeBBCode(TitleText);
        BodyMarkup = ItemRarityPresentation.EscapeBBCode(BodyText);
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
        RefreshVisualState();
    }

    public void ShowShortcutTooltip(string title, string body, Vector2 screenPos)
    {
        TitleText = title;
        BodyText = body;
        BodyText = string.Join("\n", ClampLines(BodyText.Split('\n'), 9));
        TitleMarkup = ItemRarityPresentation.EscapeBBCode(title);
        BodyMarkup = ItemRarityPresentation.EscapeBBCode(BodyText);
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
        RefreshVisualState();
    }

    public Vector2 ResolveBottomRightPosition(Vector2 viewportSize, float margin = 24f)
    {
        return ClampToViewport(new Vector2(viewportSize.X - Size.X - margin, viewportSize.Y - Size.Y - margin), viewportSize);
    }

    public new void Hide()
    {
        Visible = false;
        TitleText = string.Empty;
        BodyText = string.Empty;
        TitleMarkup = string.Empty;
        BodyMarkup = string.Empty;
        ScreenPosition = Vector2.Zero;
        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _titleLabel is not null && _bodyLabel is not null)
        {
            return;
        }

        _panel = new Panel
        {
            Name = "Panel",
            Size = Size,
            Modulate = UiStyle.GoldTrim(),
        };
        _background = new ColorRect { Name = "Background", Color = UiStyle.PanelInner(0.98f) };
        _borderTop = new ColorRect { Name = "BorderTop", Color = UiStyle.BorderActive() };
        _borderBottom = new ColorRect { Name = "BorderBottom", Color = UiStyle.BorderActive() };
        _borderLeft = new ColorRect { Name = "BorderLeft", Color = UiStyle.BorderActive() };
        _borderRight = new ColorRect { Name = "BorderRight", Color = UiStyle.BorderActive() };
        _titleLabel = new RichTextLabel
        {
            Name = "TitleLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), 32f),
            BbcodeEnabled = true,
            Modulate = UiStyle.BrightGold(),
        };
        _bodyLabel = new RichTextLabel
        {
            Name = "BodyLabel",
            Position = new Vector2(PanelPadding, PanelPadding + 38f),
            Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), Math.Max(0f, Size.Y - 48f - PanelPadding)),
            BbcodeEnabled = true,
            Modulate = UiStyle.Parchment(),
        };
        _panel.AddChild(_background);
        _panel.AddChild(_borderTop);
        _panel.AddChild(_borderBottom);
        _panel.AddChild(_borderLeft);
        _panel.AddChild(_borderRight);
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_bodyLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _titleLabel is null || _bodyLabel is null || _background is null || _borderTop is null || _borderBottom is null || _borderLeft is null || _borderRight is null)
        {
            return;
        }

        _panel.Visible = Visible;
        _panel.Position = ScreenPosition;
        _panel.Size = Size;
        _background.Position = Vector2.Zero;
        _background.Size = Size;
        _borderTop.Position = Vector2.Zero;
        _borderTop.Size = new Vector2(Size.X, 1f);
        _borderBottom.Position = new Vector2(0f, Size.Y - 1f);
        _borderBottom.Size = new Vector2(Size.X, 1f);
        _borderLeft.Position = Vector2.Zero;
        _borderLeft.Size = new Vector2(1f, Size.Y);
        _borderRight.Position = new Vector2(Size.X - 1f, 0f);
        _borderRight.Size = new Vector2(1f, Size.Y);
        _titleLabel.Visible = Visible;
        _titleLabel.Position = new Vector2(PanelPadding, PanelPadding);
        _titleLabel.Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), 32f);
        _titleLabel.Clear();
        _titleLabel.AppendText(TitleMarkup);
        _bodyLabel.Visible = Visible;
        _bodyLabel.Position = new Vector2(PanelPadding, PanelPadding + 38f);
        _bodyLabel.Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), Math.Max(0f, Size.Y - 48f - PanelPadding));
        _bodyLabel.Clear();
        _bodyLabel.AppendText(BodyMarkup);
    }

    private static string BuildItemBodyMarkup(ItemTemplate template, IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.BorderActive())}]────────────────────────[/color]");
        if (lines.Count > 0)
        {
            builder.AppendLine(ItemRarityPresentation.WrapWithColor(lines[0], template.Rarity));
        }

        if (lines.Count > 1)
        {
            builder.AppendLine($"[i][color={UiStyle.ToHex(UiStyle.MutedText())}]{ItemRarityPresentation.EscapeBBCode(lines[1])}[/color][/i]");
            builder.AppendLine($"[color={UiStyle.ToHex(UiStyle.BorderActive())}]────────────────────────[/color]");
        }

        for (var i = 2; i < lines.Count; i++)
        {
            var line = lines[i];
            var color = line.StartsWith("Equipped:", System.StringComparison.Ordinal)
                ? UiStyle.ToHex(UiStyle.ActiveGreen())
                : line.Contains(':', System.StringComparison.Ordinal) && (line.Contains('+') || line.Contains('-'))
                    ? UiStyle.ToHex(line.Contains('-') ? UiStyle.DangerRed() : UiStyle.ActiveGreen())
                    : UiStyle.ToHex(UiStyle.Parchment());
            builder.AppendLine($"[color={color}]{ItemRarityPresentation.EscapeBBCode(line)}[/color]");
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> ClampLines(IReadOnlyList<string> lines, int maxLines)
    {
        if (lines.Count <= maxLines)
        {
            return lines;
        }

        var visible = new List<string>();
        for (var i = 0; i < maxLines - 1; i++)
        {
            visible.Add(lines[i]);
        }

        visible.Add("...");
        return visible;
    }

    private Vector2 ClampToScreen(Vector2 position)
    {
        var viewport = GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1600f, 900f);
        return ClampToViewport(position, viewport);
    }

    private Vector2 ClampToViewport(Vector2 position, Vector2 viewport)
    {
        var x = position.X;
        var y = position.Y;

        if (x + Size.X > viewport.X)
        {
            x -= Size.X + 18f;
        }

        if (y + Size.Y > viewport.Y)
        {
            y = viewport.Y - Size.Y;
        }

        if (x < 0f)
        {
            x = 0f;
        }

        if (y < 0f)
        {
            y = 0f;
        }

        return new Vector2(x, y);
    }
}
