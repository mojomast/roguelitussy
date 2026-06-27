using Godot;

namespace Godotussy;

public static class UiStyle
{
    public static Color DeepBlack(float alpha = 1f)
        => new(0.04f, 0.03f, 0.03f, alpha);

    public static Color PanelBlack(float alpha = 1f)
        => new(0.08f, 0.07f, 0.06f, alpha);

    public static Color PanelInner(float alpha = 1f)
        => new(0.11f, 0.10f, 0.09f, alpha);

    public static Color CathedralBlack(float alpha = 1f)
        => new(0.13f, 0.12f, 0.10f, alpha);

    public static Color PanelHighlight(float alpha = 1f)
        => new(0.16f, 0.14f, 0.11f, alpha);

    public static Color SlotBackground(float alpha = 1f)
        => new(0.09f, 0.08f, 0.07f, alpha);

    public static Color SlotHover(float alpha = 1f)
        => new(0.18f, 0.16f, 0.12f, alpha);

    public static Color SlotSelected(float alpha = 1f)
        => new(0.22f, 0.19f, 0.13f, alpha);

    public static Color Parchment()
        => new(0.85f, 0.82f, 0.74f);

    public static Color Parchment(float alpha) => WithAlpha(Parchment(), alpha);

    public static Color MutedText()
        => new(0.58f, 0.55f, 0.48f);

    public static Color MutedText(float alpha) => WithAlpha(MutedText(), alpha);

    public static Color FaintText()
        => new(0.35f, 0.33f, 0.28f);

    public static Color FaintText(float alpha) => WithAlpha(FaintText(), alpha);

    public static Color InverseText()
        => new(0.07f, 0.06f, 0.05f);

    public static Color BrightGold()
        => new(0.87f, 0.72f, 0.28f);

    public static Color BrightGold(float alpha) => WithAlpha(BrightGold(), alpha);

    public static Color DimGold()
        => new(0.52f, 0.42f, 0.16f);

    public static Color DimGold(float alpha) => WithAlpha(DimGold(), alpha);

    public static Color ActiveGreen()
        => new(0.42f, 0.72f, 0.28f);

    public static Color WarningOrange()
        => new(0.82f, 0.52f, 0.18f);

    public static Color DangerRed()
        => new(0.78f, 0.25f, 0.22f);

    public static Color EnergyBlue()
        => new(0.30f, 0.55f, 0.82f);

    public static Color RarityCommon()
        => new(0.62f, 0.60f, 0.55f);

    public static Color RarityUncommon()
        => new(0.30f, 0.70f, 0.42f);

    public static Color RarityRare()
        => new(0.30f, 0.55f, 0.85f);

    public static Color RarityEpic()
        => new(0.65f, 0.38f, 0.85f);

    public static Color RarityLegendary()
        => new(0.87f, 0.62f, 0.12f);

    public static Color BorderSubtle()
        => new(0.22f, 0.20f, 0.17f, 0.9f);

    public static Color BorderActive()
        => new(0.42f, 0.38f, 0.28f, 1.0f);

    public static Color HpColor(float fraction) => fraction switch
    {
        >= 0.6f => ActiveGreen(),
        >= 0.3f => WarningOrange(),
        _ => DangerRed(),
    };

    public static Color GoldTrim(float alpha = 1f) => DimGold(alpha);

    public static Color BloodRed(float alpha = 1f) => WithAlpha(DangerRed(), alpha);

    public static Color BloodRedBright(float alpha = 1f) => WithAlpha(DangerRed(), alpha);

    public static Color MapLineBlue(float alpha = 1f) => WithAlpha(EnergyBlue(), alpha);

    public static Color MapGold(float alpha = 1f) => BrightGold(alpha);

    public static string CommonHex => ToHex(RarityCommon());

    public static string UncommonHex => ToHex(RarityUncommon());

    public static string RareHex => ToHex(RarityRare());

    public static string EpicHex => ToHex(RarityEpic());

    public static string LegendaryHex => ToHex(RarityLegendary());

    public static string ArtifactHex => ToHex(DangerRed());

    public static string ToHex(Color color)
    {
        static int Channel(float value) => (int)System.Math.Clamp(System.MathF.Round(value * 255f), 0f, 255f);
        return $"#{Channel(color.R):x2}{Channel(color.G):x2}{Channel(color.B):x2}";
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
