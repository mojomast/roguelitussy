using System;
using Godot;
using Roguelike.Core;

namespace Godotussy;

internal sealed record PlayerVisualProfile(
    Color BodyTint,
    Color AccentTint,
    Color DetailTint,
    string RaceSigil,
    string AppearanceMark,
    string Title,
    string Subtitle,
    string VariantId);

internal static class PlayerVisualCatalog
{
    private const string PlayerTexturePath = "res://Assets/Sprites/player_tiny_dungeon.png";

    public static Texture2D? GetBaseTexture() => GD.Load<Texture2D>(PlayerTexturePath);

    public static PlayerVisualProfile Resolve(IEntity entity)
    {
        var identity = entity.GetComponent<IdentityComponent>();
        return Resolve(
            identity?.RaceId,
            identity?.GenderId,
            identity?.AppearanceId,
            identity?.SpriteVariantId);
    }

    public static PlayerVisualProfile Resolve(
        string? raceId,
        string? genderId,
        string? appearanceId,
        string? spriteVariantId = null,
        string? archetypeId = null)
    {
        var race = NormalizeIdentityId(raceId, "human");
        var gender = NormalizeIdentityId(genderId, "neutral");
        var appearance = NormalizeIdentityId(appearanceId, "default");
        var variantId = string.IsNullOrWhiteSpace(spriteVariantId)
            ? ComposeVariantId(race, gender, appearance, archetypeId)
            : spriteVariantId.Trim().ToLowerInvariant();

        return new PlayerVisualProfile(
            ResolveRaceTint(race),
            ResolveGenderTint(gender),
            ResolveAppearanceTint(appearance),
            ResolveRaceSigil(race),
            ResolveAppearanceMark(appearance),
            $"{DisplayIdentity(race)} {DisplayIdentity(gender)}",
            $"{DisplayIdentity(appearance)} variant",
            variantId);
    }

    public static string ComposeVariantId(
        string? raceId,
        string? genderId,
        string? appearanceId,
        string? archetypeId)
    {
        return string.Join(
            "_",
            NormalizeIdentityId(raceId, "human"),
            NormalizeIdentityId(genderId, "neutral"),
            NormalizeIdentityId(appearanceId, "default"),
            NormalizeArchetypeId(archetypeId));
    }

    public static string BuildPreviewToken(PlayerVisualProfile profile) => $"[{profile.RaceSigil}{profile.AppearanceMark}]";

    private static Color ResolveRaceTint(string raceId)
    {
        return raceId switch
        {
            "elf" => new Color(0.35f, 0.90f, 0.55f, 1f),
            "dwarf" => new Color(0.80f, 0.65f, 0.30f, 1f),
            "orc" => new Color(0.45f, 0.75f, 0.35f, 1f),
            _ => new Color(0.25f, 0.85f, 0.35f, 1f),
        };
    }

    private static Color ResolveGenderTint(string genderId)
    {
        return genderId switch
        {
            "masculine" => new Color(0.34f, 0.58f, 0.95f, 1f),
            "feminine" => new Color(0.95f, 0.48f, 0.64f, 1f),
            _ => new Color(0.86f, 0.88f, 0.92f, 1f),
        };
    }

    private static Color ResolveAppearanceTint(string appearanceId)
    {
        return appearanceId switch
        {
            "scarred" => new Color(0.94f, 0.38f, 0.30f, 1f),
            "youthful" => new Color(0.96f, 0.94f, 0.48f, 1f),
            "weathered" => new Color(0.71f, 0.59f, 0.42f, 1f),
            _ => new Color(0.96f, 0.96f, 0.98f, 1f),
        };
    }

    private static string ResolveRaceSigil(string raceId)
    {
        return raceId switch
        {
            "elf" => "/",
            "dwarf" => "=",
            "orc" => "*",
            _ => "+",
        };
    }

    private static string ResolveAppearanceMark(string appearanceId)
    {
        return appearanceId switch
        {
            "scarred" => "!",
            "youthful" => "'",
            "weathered" => "~",
            _ => ".",
        };
    }

    private static string NormalizeIdentityId(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string NormalizeArchetypeId(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "adventurer"
            : value.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string DisplayIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value[1..].Replace('_', ' ');
    }
}