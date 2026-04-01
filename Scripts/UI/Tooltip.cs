using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class Tooltip : Control
{
    private const float PanelPadding = 12f;
    private Panel? _panel;
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
        Size = new Vector2(320f, 200f);
    }

    public override void _Ready()
    {
        EnsureVisuals();
        RefreshVisualState();
    }

    public void ShowItemTooltip(ItemTemplate template, ItemInstance instance, Vector2 screenPos,
        string? comparisonText = null)
    {
        TitleText = template.DisplayName;
        TitleMarkup = ItemRarityPresentation.WrapWithColor(template.DisplayName, template.Rarity);

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

        if (instance.StackCount > 1)
        {
            lines.Add($"Stack: {instance.StackCount}");
        }

        lines.Add($"Category: {template.Category}");

        if (!string.IsNullOrEmpty(comparisonText))
        {
            lines.Add(comparisonText);
        }

        BodyText = string.Join("\n", lines);
        BodyMarkup = BuildItemBodyMarkup(template, lines);
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
        TitleMarkup = ItemRarityPresentation.EscapeBBCode(title);
        BodyMarkup = ItemRarityPresentation.EscapeBBCode(body);
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
        };
        _titleLabel = new RichTextLabel
        {
            Name = "TitleLabel",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), 24f),
            BbcodeEnabled = true,
        };
        _bodyLabel = new RichTextLabel
        {
            Name = "BodyLabel",
            Position = new Vector2(PanelPadding, PanelPadding + 28f),
            Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), Math.Max(0f, Size.Y - 40f - PanelPadding)),
            BbcodeEnabled = true,
        };
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_bodyLabel);
        AddChild(_panel);
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();

        if (_panel is null || _titleLabel is null || _bodyLabel is null)
        {
            return;
        }

        _panel.Visible = Visible;
        _panel.Position = ScreenPosition;
        _panel.Size = Size;
        _titleLabel.Visible = Visible;
        _titleLabel.Position = new Vector2(PanelPadding, PanelPadding);
        _titleLabel.Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), 24f);
        _titleLabel.Clear();
        _titleLabel.AppendText(TitleMarkup);
        _bodyLabel.Visible = Visible;
        _bodyLabel.Position = new Vector2(PanelPadding, PanelPadding + 28f);
        _bodyLabel.Size = new Vector2(Math.Max(0f, Size.X - (PanelPadding * 2f)), Math.Max(0f, Size.Y - 40f - PanelPadding));
        _bodyLabel.Clear();
        _bodyLabel.AppendText(BodyMarkup);
    }

    private static string BuildItemBodyMarkup(ItemTemplate template, IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine(ItemRarityPresentation.WrapWithColor(lines[0], template.Rarity));
        for (var i = 1; i < lines.Count; i++)
        {
            builder.AppendLine(ItemRarityPresentation.EscapeBBCode(lines[i]));
        }

        return builder.ToString().TrimEnd();
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
            x = viewport.X - Size.X;
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