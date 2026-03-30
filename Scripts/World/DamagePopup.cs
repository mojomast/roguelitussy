using Godot;

namespace Godotussy;

public partial class DamagePopup : Label
{
    public const float Duration = 0.8f;
    public const float RiseDistance = 24f;

    public bool IsCritical { get; private set; }

    public bool IsHeal { get; private set; }

    public bool IsMiss { get; private set; }

    public void Setup(int amount, bool isCrit, bool isHeal, bool isMiss = false)
    {
        IsCritical = isCrit;
        IsHeal = isHeal;
        IsMiss = isMiss;
        Text = isMiss ? "MISS" : isHeal ? $"+{amount}" : $"{amount}";
        ZIndex = 100;
    }
}