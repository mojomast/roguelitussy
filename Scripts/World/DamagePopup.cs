using Godot;

namespace Roguelike.Godot;

public partial class DamagePopup : Label
{
    private const float Duration = 0.8f;
    private const float RiseDistance = 24f;

    private static readonly Color DamageColor = new(1f, 0.2f, 0.2f);
    private static readonly Color HealColor = new(0.2f, 1f, 0.3f);
    private static readonly Color CritColor = new(1f, 1f, 0f);

    public void Setup(int amount, bool isCrit, bool isHeal)
    {
        Text = isHeal ? $"+{amount}" : $"{amount}";

        if (isHeal)
            AddThemeColorOverride("font_color", HealColor);
        else if (isCrit)
            AddThemeColorOverride("font_color", CritColor);
        else
            AddThemeColorOverride("font_color", DamageColor);

        if (isCrit)
            Scale = Vector2.One * 1.5f;

        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ZIndex = 100;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "position:y", Position.Y - RiseDistance, Duration)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0f, Duration)
            .SetEase(Tween.EaseType.In)
            .SetDelay(Duration * 0.4f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
