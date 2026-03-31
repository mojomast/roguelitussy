using System;
using Roguelike.Core;

namespace Godotussy;

public static class ItemRarityPresentation
{
    public static string Normalize(string? rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
        {
            return "common";
        }

        return rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "common",
            "uncommon" => "uncommon",
            "rare" => "rare",
            "epic" => "epic",
            "legendary" => "legendary",
            "artifact" => "artifact",
            var other => other,
        };
    }

    public static string ResolveDisplayLabel(string? rarity)
    {
        return Normalize(rarity) switch
        {
            "common" => "Common",
            "uncommon" => "Uncommon",
            "rare" => "Rare",
            "epic" => "Epic",
            "legendary" => "Legendary",
            "artifact" => "Artifact",
            var other when other.Length > 0 => char.ToUpperInvariant(other[0]) + other[1..],
            _ => "Common",
        };
    }

    public static string ResolveHexColor(string? rarity)
    {
        return Normalize(rarity) switch
        {
            "uncommon" => "#78c27d",
            "rare" => "#63a9ff",
            "epic" => "#c987ff",
            "legendary" => "#f1b24a",
            "artifact" => "#ff7a59",
            _ => "#d7d7d7",
        };
    }

    public static bool IsHighlighted(string? rarity)
    {
        return !string.Equals(Normalize(rarity), "common", StringComparison.Ordinal);
    }

    public static string WrapWithColor(string text, string? rarity)
    {
        return $"[color={ResolveHexColor(rarity)}]{EscapeBBCode(text)}[/color]";
    }

    public static string EscapeBBCode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            buffer.Append(character switch
            {
                '[' => "[lb]",
                ']' => "[rb]",
                _ => character,
            });
        }

        return buffer.ToString();
    }

    public static string ResolvePickupCallout(ItemTemplate template)
    {
        return IsHighlighted(template.Rarity)
            ? $"{ResolveDisplayLabel(template.Rarity).ToLowerInvariant()} loot"
            : "gear";
    }
}