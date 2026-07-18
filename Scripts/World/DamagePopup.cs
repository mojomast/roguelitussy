using Godot;

namespace Godotussy;

public partial class DamagePopup : Label
{
    public const float Duration = 0.8f;
    public const float RiseDistance = 24f;
    public const int NormalFontSize = 14;
    public const int CriticalFontSize = 20;

    public static readonly Color NormalColor = new(1f, 1f, 1f, 1f);
    public static readonly Color CriticalColor = new(1f, 0.62f, 0.16f, 1f);
    public static readonly Color HealColor = new(0.38f, 0.9f, 0.42f, 1f);
    public static readonly Color MissColor = new(0.7f, 0.7f, 0.74f, 1f);

    public bool IsCritical { get; private set; }

    public bool IsHeal { get; private set; }

    public bool IsMiss { get; private set; }

    /// <summary>Tint the popup keeps for its whole lifetime; the fade-out only scales its alpha.</summary>
    public Color BaseColor { get; private set; } = NormalColor;

    public void Setup(int amount, bool isCrit, bool isHeal, bool isMiss = false)
    {
        IsCritical = isCrit;
        IsHeal = isHeal;
        IsMiss = isMiss;
        Text = isMiss ? "MISS" : isHeal ? $"+{amount}" : isCrit ? $"{amount}!" : $"{amount}";
        BaseColor = isMiss ? MissColor : isHeal ? HealColor : isCrit ? CriticalColor : NormalColor;
        Modulate = BaseColor;
        AddThemeFontSizeOverride("font_size", isCrit ? CriticalFontSize : NormalFontSize);
        ZIndex = 100;
    }

    public void SetupText(string text, Color color)
    {
        IsCritical = false;
        IsHeal = false;
        IsMiss = false;
        Text = text;
        BaseColor = color;
        Modulate = BaseColor;
        AddThemeFontSizeOverride("font_size", NormalFontSize);
        ZIndex = 100;
    }
}
