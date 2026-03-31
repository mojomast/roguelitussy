using System;
using System.Linq;
using Godot;

namespace Godotussy;

internal static class OverlayLayoutHelper
{
    public static Vector2 FitPanelSize(Vector2 viewportSize, Vector2 desiredSize, float outerMargin = 24f)
    {
        var maxWidth = Math.Max(0f, viewportSize.X - (outerMargin * 2f));
        var maxHeight = Math.Max(0f, viewportSize.Y - (outerMargin * 2f));

        return new Vector2(
            maxWidth <= 0f ? desiredSize.X : Math.Min(desiredSize.X, maxWidth),
            maxHeight <= 0f ? desiredSize.Y : Math.Min(desiredSize.Y, maxHeight));
    }

    public static Vector2 CenterInViewport(Vector2 viewportSize, Vector2 panelSize)
    {
        return new Vector2(
            Math.Max(0f, (viewportSize.X - panelSize.X) * 0.5f),
            Math.Max(0f, (viewportSize.Y - panelSize.Y) * 0.5f));
    }

    public static Vector2 MeasureMonospaceBlock(
        string text,
        float minWidth,
        float minHeight,
        float padding,
        float maxWidth,
        float glyphWidth = 8f,
        float lineHeight = 18f)
    {
        var lines = string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('\n');
        var longestLine = lines.Length == 0 ? 0 : lines.Max(line => line.Length);
        var width = Math.Max(minWidth, (longestLine * glyphWidth) + (padding * 2f));
        var height = Math.Max(minHeight, (Math.Max(1, lines.Length) * lineHeight) + (padding * 2f));

        if (maxWidth > 0f)
        {
            width = Math.Min(width, maxWidth);
        }

        return new Vector2(width, height);
    }
}