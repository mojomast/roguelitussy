using System.Collections.Generic;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class Tooltip : Control
{
    public string TitleText { get; private set; } = string.Empty;

    public string BodyText { get; private set; } = string.Empty;

    public Vector2 ScreenPosition { get; private set; }

    public Tooltip()
    {
        Name = "Tooltip";
        Visible = false;
        Size = new Vector2(260f, 140f);
    }

    public void ShowItemTooltip(ItemTemplate template, ItemInstance instance, Vector2 screenPos)
    {
        TitleText = template.DisplayName;

        var lines = new List<string> { template.Description };
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
        BodyText = string.Join("\n", lines);
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
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
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
    }

    public void ShowShortcutTooltip(string title, string body, Vector2 screenPos)
    {
        TitleText = title;
        BodyText = body;
        ScreenPosition = ClampToScreen(screenPos);
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
        TitleText = string.Empty;
        BodyText = string.Empty;
    }

    private Vector2 ClampToScreen(Vector2 position)
    {
        var viewport = GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
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