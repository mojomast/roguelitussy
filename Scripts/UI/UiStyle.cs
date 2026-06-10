using Godot;

namespace Godotussy;

public static class UiStyle
{
    public static Color CathedralBlack(float alpha = 1f) => new(0.035f, 0.027f, 0.02f, alpha);

    public static Color PanelBlack(float alpha = 1f) => new(0.071f, 0.051f, 0.039f, alpha);

    public static Color PanelInner(float alpha = 1f) => new(0.094f, 0.071f, 0.059f, alpha);

    public static Color PanelHighlight(float alpha = 1f) => new(0.227f, 0.141f, 0.082f, alpha);

    public static Color GoldTrim(float alpha = 1f) => new(0.624f, 0.478f, 0.212f, alpha);

    public static Color BrightGold(float alpha = 1f) => new(0.835f, 0.667f, 0.333f, alpha);

    public static Color Parchment(float alpha = 1f) => new(0.847f, 0.784f, 0.647f, alpha);

    public static Color MutedText(float alpha = 1f) => new(0.604f, 0.541f, 0.424f, alpha);

    public static Color BloodRed(float alpha = 1f) => new(0.557f, 0.086f, 0.086f, alpha);

    public static Color BloodRedBright(float alpha = 1f) => new(0.765f, 0.157f, 0.125f, alpha);

    public static Color MapLineBlue(float alpha = 1f) => new(0.259f, 0.435f, 0.561f, alpha);

    public static Color MapGold(float alpha = 1f) => new(0.694f, 0.541f, 0.263f, alpha);

    public const string CommonHex = "#d8c8a5";

    public const string UncommonHex = "#5fbf5a";

    public const string RareHex = "#4f83d1";

    public const string EpicHex = "#9a5fd0";

    public const string LegendaryHex = "#d08a2a";

    public const string ArtifactHex = "#b62020";
}
