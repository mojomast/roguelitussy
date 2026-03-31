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
    string VariantId,
    string TextureKey);

internal static class PlayerVisualCatalog
{
    private const string SpriteBasePath = "res://Assets/Sprites/0x72/";
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> TextureCache = new();

    public static Texture2D? GetBaseTexture(IEntity entity)
    {
        return GetBaseTexture(Resolve(entity));
    }

    public static Texture2D? GetBaseTexture(PlayerVisualProfile profile)
    {
        return profile.TextureKey switch
        {
            "vanguard_stalwart" => Load(SpriteBasePath + "Knight_Male_Idle_1.png"),
            "vanguard_knight" => Load(SpriteBasePath + "Knight_Female_Idle_1.png"),
            "skirmisher_quickblade" => Load(SpriteBasePath + "Elf_Male_Idle_1.png"),
            "skirmisher_ranger" => Load(SpriteBasePath + "Elf_Female_Idle_1.png"),
            "mystic_apprentice" => Load(SpriteBasePath + "Wizzard_Male_Idle_1.png"),
            "orc_raider" => Load(SpriteBasePath + "Orc_Warrior_Idle_1.png"),
            "orc_shaman" => Load(SpriteBasePath + "Orc_Shaman_Idle_1.png"),
            _ => Load(SpriteBasePath + "Wizzard_Female_Idle_1.png"),
        };
    }

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
            ResolveSpriteLabel(race, archetypeId),
            variantId,
            ResolveTextureKey(race, gender, archetypeId));
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

    private static string ResolveTextureKey(string raceId, string genderId, string? archetypeId)
    {
        var archetype = NormalizeArchetypeId(archetypeId);
        if (raceId == "dwarf")
        {
            return archetype == "mystic"
                ? genderId == "feminine" ? "mystic_wizard" : "mystic_apprentice"
                : genderId == "feminine" ? "vanguard_knight" : "vanguard_stalwart";
        }

        if (raceId == "elf")
        {
            return archetype switch
            {
                "vanguard" => "vanguard_knight",
                "mystic" => genderId == "feminine" ? "mystic_wizard" : "mystic_apprentice",
                _ => genderId == "feminine" ? "skirmisher_ranger" : "skirmisher_quickblade",
            };
        }

        if (raceId == "orc")
        {
            return archetype == "mystic" ? "orc_shaman" : "orc_raider";
        }

        return archetype switch
        {
            "vanguard" => genderId == "feminine" ? "vanguard_knight" : "vanguard_stalwart",
            "skirmisher" => genderId == "feminine" ? "skirmisher_ranger" : "skirmisher_quickblade",
            "mystic" => genderId == "feminine" ? "mystic_wizard" : "mystic_apprentice",
            _ => "mystic_apprentice",
        };
    }

    private static string ResolveSpriteLabel(string raceId, string? archetypeId)
    {
        var archetype = NormalizeArchetypeId(archetypeId);
        return archetype switch
        {
            "vanguard" when raceId == "dwarf" => "Stoneguard portrait",
            "vanguard" => "Knight portrait",
            "skirmisher" when raceId == "orc" => "Raider portrait",
            "skirmisher" => "Ranger portrait",
            "mystic" when raceId == "orc" => "Shaman portrait",
            "mystic" => "Mystic portrait",
            _ => "Adventurer portrait",
        };
    }

    private static Texture2D? Load(string path)
    {
        if (!TextureCache.TryGetValue(path, out var texture))
        {
            texture = GD.Load<Texture2D>(path);
            TextureCache[path] = texture;
        }

        return texture;
    }

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