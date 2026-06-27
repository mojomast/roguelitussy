using System;
using Godot;
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

    public static string ResolveAbbreviation(string? rarity)
    {
        return Normalize(rarity) switch
        {
            "common" => "C",
            "uncommon" => "U",
            "rare" => "R",
            "epic" => "E",
            "legendary" => "L",
            "artifact" => "A",
            _ => "?",
        };
    }

    public static string ResolveBracketedAbbreviation(string? rarity)
    {
        return $"[{ResolveAbbreviation(rarity)}]";
    }

    public static string ResolveBracketedAbbreviationMarkup(string? rarity)
    {
        return $"[lb]{ResolveAbbreviation(rarity)}[rb]";
    }

    public static string ResolveDecoratedName(string itemName, string? rarity)
    {
        return $"{ResolveBracketedAbbreviation(rarity)} {itemName}";
    }

    public static string ResolveDecoratedNameMarkup(string itemName, string? rarity)
    {
        return $"{ResolveBracketedAbbreviationMarkup(rarity)} {EscapeBBCode(itemName)}";
    }

    public static string WrapDecoratedNameWithColor(string itemName, string? rarity)
    {
        return $"[color={ResolveHexColor(rarity)}]{ResolveDecoratedNameMarkup(itemName, rarity)}[/color]";
    }

    public static string ResolveHexColor(string? rarity)
    {
        return UiStyle.ToHex(ResolveColor(rarity));
    }

    public static Color ResolveColor(string? rarity)
    {
        return Normalize(rarity) switch
        {
            "uncommon" => UiStyle.RarityUncommon(),
            "rare" => UiStyle.RarityRare(),
            "epic" => UiStyle.RarityEpic(),
            "legendary" => UiStyle.RarityLegendary(),
            "artifact" => UiStyle.DangerRed(),
            _ => UiStyle.RarityCommon(),
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
