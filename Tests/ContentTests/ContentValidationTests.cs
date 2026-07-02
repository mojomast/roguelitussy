using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.ContentTests;

public sealed class ContentValidationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Content.Loads and validates all JSON", LoadsAndValidatesAllJson);
        registry.Add("Content.Projects contract templates", ProjectsContractTemplates);
        registry.Add("Content.Cross references remain intact", CrossReferencesRemainIntact);
        registry.Add("Content.Enemy gold ranges are valid", EnemyGoldRangesAreValid);
        registry.Add("Content.Item stats and effects are runtime-supported", ItemStatsAndEffectsAreRuntimeSupported);
        registry.Add("Content.Character creation starter items resolve", CharacterCreationStarterItemsResolve);
        registry.Add("Content.Authored art paths resolve to committed files", AuthoredArtPathsResolveToCommittedFiles);
        registry.Add("Content.Depth filters stay stable", DepthFiltersStayStable);
        registry.Add("Content.Loads from in-memory JSON documents", LoadsFromInMemoryJsonDocuments);
    }

    private static void LoadsAndValidatesAllJson()
    {
        var content = LoadContent();

        Expect.True(content.IsValid, FormatErrors(content));
        Expect.Equal(29, content.ItemDefinitions.Count, "Expected the full item set to load");
        Expect.Equal(21, content.EnemyDefinitions.Count, "Expected the full enemy set to load");
        Expect.Equal(26, content.AbilityDefinitions.Count, "Expected the full ability set to load");
        Expect.Equal(20, content.PerkDefinitions.Count, "Expected the initial perk set to load");
        Expect.Equal(2, content.DialogueDefinitions.Count, "Expected the full dialog set to load");
        Expect.Equal(2, content.NpcDefinitions.Count, "Expected the full NPC set to load");
        Expect.Equal(10, content.StatusEffects.Count, "Expected the full status effect set to load");
        Expect.True(content.RoomPrefabs.Count >= 10, "Expected at least the baseline room prefab set to load");
        Expect.Equal(27, content.LootTables.Count, "Expected the full loot table set to load");
        Expect.Equal(6, content.TrapDefinitions.Count, "Expected the baseline trap set to load");
        Expect.Equal(20, content.RelicTemplates.Count, "Expected the full relic set to load");
        Expect.Equal(6, content.FloorEvents.Count, "Expected the floor event catalogue to load");
        Expect.Equal(15, content.Synergies.Count, "Expected the synergy catalogue to load");
        Expect.Equal(10, content.AscensionModifiers.Count, "Expected the ascension modifier catalogue to load");
        Expect.Equal(7, content.DailyModifiers.Count, "Expected the daily modifier catalogue to load");
        Expect.Equal(40, content.NarrativeTemplates.Count, "Expected the narrative template catalogue to load");
        Expect.Equal(3, content.Factions.Count, "Expected the faction catalogue to load");
    }

    private static void LoadsFromInMemoryJsonDocuments()
    {
        var contentDirectory = ContentLoader.FindContentDirectory();
        var documents = new Dictionary<string, string>();
        foreach (var fileName in ContentLoader.RequiredFileNames)
        {
            documents[fileName] = File.ReadAllText(Path.Combine(contentDirectory, fileName));
        }

        var content = ContentLoader.LoadFromJsonDocuments("res://Content", documents, throwOnValidationErrors: false);

        Expect.True(content.IsValid, FormatErrors(content));
        Expect.Equal("res://Content", content.ContentDirectory, "Document-backed content should preserve the supplied content source label.");
        Expect.True(content.TryGetItemTemplate("dungeon_key", out _), "Document-backed content should project item templates.");
        Expect.True(content.TryGetTrapTemplate("spike_trap", out _), "Document-backed content should project trap templates.");
    }

    private static void ProjectsContractTemplates()
    {
        var content = LoadContent();

        Expect.True(content.TryGetItemTemplate("potion_health", out var potion), "Health potion template should project into the contract surface");
        Expect.Equal(ItemCategory.Consumable, potion.Category, "Health potion should project as a consumable");
        Expect.Equal(10, potion.MaxStack, "Health potion should preserve stack size");
        Expect.Equal("heal", potion.UseEffect ?? string.Empty, "Health potion should preserve the on-use effect");
        Expect.Equal("common", potion.Rarity, "Health potion should preserve the projected rarity tier");

        Expect.True(content.TryGetEnemyTemplate("orc_brute", out var orc), "Orc Brute should project into the contract surface");
        Expect.Equal("melee_rusher", orc.BrainType, "Enemy AI types should map onto the frozen brain identifiers");
        Expect.Equal(35, orc.BaseStats.MaxHP, "Enemy HP should map into the base stats block");
        Expect.Equal(80, orc.BaseStats.Speed, "Enemy speed should respect the engine-scale contract");
        Expect.True(orc.GoldMin >= 0 && orc.GoldMin <= orc.GoldMax, "Enemy gold range should project into the contract surface");

        Expect.True(content.TryGetNpcTemplate("quartermaster_vale", out var vendor), "Quartermaster Vale should project into the NPC surface");
        Expect.True(vendor.IsMerchant, "Quartermaster Vale should project merchant stock");
        Expect.Equal("merchant_intro", vendor.DialogueId, "NPC dialog ids should survive projection");
        Expect.True(vendor.MerchantOffers?.Count >= 10, "Quartermaster stock should cover more than the original three-item starter set.");
        Expect.True(vendor.MerchantOffers?.Any(offer => offer.ItemTemplateId.StartsWith("potion_", System.StringComparison.Ordinal)) == true, "Quartermaster stock should include consumables.");
        Expect.True(vendor.MerchantOffers?.Any(offer => offer.ItemTemplateId.StartsWith("scroll_", System.StringComparison.Ordinal)) == true, "Quartermaster stock should include scrolls.");
        Expect.True(vendor.MerchantOffers?.Any(offer => offer.ItemTemplateId is "boots_leather" or "helm_iron" or "shield_wooden") == true, "Quartermaster stock should include armor choices.");

        Expect.True(content.TryGetPerkTemplate("quartermasters_eye", out var perk), "Quartermaster's Eye should project into the perk surface");
        Expect.Equal(2, perk.UnlockLevel, "Perk unlock levels should survive projection");

        Expect.True(content.TryGetDialogueTemplate("merchant_intro", out var dialog), "Merchant intro dialog should project into the dialogue surface");
        Expect.True(dialog.Nodes.ContainsKey(dialog.StartNodeId), "Dialog templates should retain their start node");
        Expect.True(dialog.StartNodeIds.Count >= 3, "Merchant dialog should project authored greeting variants.");

        Expect.True(content.TryGetTrapTemplate("spike_trap", out var spikeTrap), "Spike trap should project into the trap surface");
        Expect.Equal("spike_trap", spikeTrap?.AbilityId ?? string.Empty, "Trap templates should preserve their referenced ability");
        Expect.True(content.TryGetSynergy("leech_vampire", out var synergy), "Synergy content should project into the content surface");
        Expect.Equal("Leech Vampire", synergy.DisplayName, "Synergy display names should project.");
        Expect.True(content.TryGetAscensionModifier("a02_shop_prices", out var ascension), "Ascension modifiers should project into the content surface");
        Expect.Equal(2, ascension.AscensionLevel, "Ascension levels should project.");
        Expect.True(content.TryGetDailyModifier("friday_relic_rush", out var daily), "Daily modifiers should project into the content surface");
        Expect.Equal(5, daily.DayOfWeek, "Daily modifier day indexes should project.");
        Expect.True(content.TryGetFaction("merchants_guild", out var faction), "Factions should project into the content surface");
        Expect.Equal("Merchants' Guild", faction.DisplayName, "Faction display names should project.");
    }

    private static void CrossReferencesRemainIntact()
    {
        var content = LoadContent();

        foreach (var item in content.ItemDefinitions.Values)
        {
            foreach (var effect in item.Effects)
            {
                if (effect.AbilityId is not null)
                {
                    Expect.True(content.AbilityDefinitions.ContainsKey(effect.AbilityId), $"Item '{item.Id}' should reference a known ability");
                }

                if (effect.StatusEffect is not null)
                {
                    Expect.True(content.StatusEffects.ContainsKey(effect.StatusEffect), $"Item '{item.Id}' should reference a known status effect");
                }
            }
        }

        foreach (var enemy in content.EnemyDefinitions.Values)
        {
            foreach (var ability in enemy.Abilities)
            {
                Expect.True(content.AbilityDefinitions.ContainsKey(ability.AbilityId), $"Enemy '{enemy.Id}' should reference a known ability");
            }

            if (enemy.LootTableId is not null)
            {
                Expect.True(content.LootTables.ContainsKey(enemy.LootTableId), $"Enemy '{enemy.Id}' should reference a known loot table");
            }
        }

        foreach (var npc in content.NpcDefinitions.Values)
        {
            Expect.True(content.DialogueDefinitions.ContainsKey(npc.DialogueId), $"Npc '{npc.Id}' should reference a known dialog");
            foreach (var stock in npc.Stock)
            {
                Expect.True(content.ItemDefinitions.ContainsKey(stock.ItemId), $"Npc '{npc.Id}' stock should reference a known item");
            }
        }

        foreach (var trap in content.TrapDefinitions.Values)
        {
            Expect.True(content.AbilityDefinitions.ContainsKey(trap.AbilityId!), $"Trap '{trap.Id}' should reference a known ability");
        }

        foreach (var room in content.RoomPrefabs.Values)
        {
            foreach (var spawnPoint in room.SpawnPoints)
            {
                if (string.Equals(spawnPoint.Type, "trap", System.StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(spawnPoint.TrapId))
                {
                    Expect.True(content.TrapDefinitions.ContainsKey(spawnPoint.TrapId), $"Room '{room.Id}' trap spawn should reference a known trap");
                }
            }
        }
    }

    private static void ItemStatsAndEffectsAreRuntimeSupported()
    {
        var content = LoadContent();

        foreach (var item in content.ItemDefinitions.Values)
        {
            foreach (var stat in item.Stats.Keys)
            {
                Expect.True(IsRuntimeSupportedStat(stat), $"Item '{item.Id}' stat '{stat}' should be applied by runtime equipment or item-use rules");
            }

            foreach (var effect in item.Effects)
            {
                Expect.False(string.Equals(effect.Type, "passive", System.StringComparison.Ordinal), $"Item '{item.Id}' passive effect should not be authored until runtime support exists");
            }
        }
    }

    private static void CharacterCreationStarterItemsResolve()
    {
        var content = LoadContent();
        foreach (var itemId in MainMenu.EnumerateAuthoredStartingItemIds().Distinct())
        {
            Expect.True(content.TryGetItemTemplate(itemId, out var template), $"Starter item '{itemId}' should resolve in loaded content.");
            Expect.False(string.IsNullOrWhiteSpace(template.DisplayName), $"Starter item '{itemId}' should have a display name.");
            Expect.False(string.IsNullOrWhiteSpace(template.Description), $"Starter item '{itemId}' should have a description.");
        }
    }

    private static void DepthFiltersStayStable()
    {
        var content = LoadContent();
        var earlyItems = content.GetAvailableItems(0).Select(item => item.TemplateId).ToHashSet();
        var lateItems = content.GetAvailableItems(4).Select(item => item.TemplateId).ToHashSet();
        var earlyEnemies = content.GetAvailableEnemies(0).Select(enemy => enemy.TemplateId).ToHashSet();
        var lateEnemies = content.GetAvailableEnemies(4).Select(enemy => enemy.TemplateId).ToHashSet();

        Expect.True(earlyItems.Contains("potion_health"), "Depth 0 item pool should include the basic health potion");
        Expect.False(earlyItems.Contains("sword_flame"), "Depth 0 item pool should not include rare level 5 weapons");
        Expect.True(lateItems.Contains("sword_flame"), "Depth 4 item pool should unlock level 5 weapons");

        Expect.True(earlyEnemies.Contains("rat"), "Depth 0 enemy pool should include early-game vermin");
        Expect.False(earlyEnemies.Contains("wraith"), "Depth 0 enemy pool should exclude late-game enemies");
        Expect.True(lateEnemies.Contains("wraith"), "Depth 4 enemy pool should include late-game enemies");
    }

    private static void AuthoredArtPathsResolveToCommittedFiles()
    {
        var content = LoadContent();
        foreach (var item in content.ItemDefinitions.Values)
        {
            ExpectResPathExists(content, item.SpritePath, $"Item '{item.Id}' sprite_path");
        }

        foreach (var enemy in content.EnemyDefinitions.Values)
        {
            ExpectResPathExists(content, enemy.SpritePath, $"Enemy '{enemy.Id}' sprite_path");
        }

        foreach (var status in content.StatusEffects.Values)
        {
            ExpectResPathExists(content, status.IconPath, $"Status effect '{status.Id}' icon_path");
        }

        foreach (var trap in content.TrapDefinitions.Values)
        {
            ExpectResPathExists(content, trap.SpritePath, $"Trap '{trap.Id}' sprite_path");
        }
    }

    private static void EnemyGoldRangesAreValid()
    {
        var content = LoadContent();

        foreach (var enemy in content.EnemyDefinitions.Values)
        {
            Expect.True(enemy.GoldMin >= 0, $"Enemy '{enemy.Id}' gold_min must be non-negative");
            Expect.True(enemy.GoldMax >= 0, $"Enemy '{enemy.Id}' gold_max must be non-negative");
            Expect.True(enemy.GoldMin <= enemy.GoldMax, $"Enemy '{enemy.Id}' gold_min must not exceed gold_max");
        }
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        return content;
    }

    private static bool IsRuntimeSupportedStat(string stat)
    {
        return stat is "damage_min"
            or "damage_max"
            or "accuracy"
            or "speed_modifier"
            or "crit_chance"
            or "defense"
            or "evasion"
            or "fov_bonus"
            or "attack"
            or "hp"
            or "max_hp";
    }

    private static void ExpectResPathExists(ContentLoader content, string resPath, string label)
    {
        Expect.True(resPath.StartsWith("res://", System.StringComparison.Ordinal), $"{label} should use a res:// path");
        var relativePath = resPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar);
        var repositoryRoot = Path.GetFullPath(Path.Combine(content.ContentDirectory, ".."));
        var filePath = Path.Combine(repositoryRoot, relativePath);
        Expect.True(File.Exists(filePath), $"{label} should resolve to a committed file: {resPath}");
    }

    private static string FormatErrors(ContentLoader content)
    {
        return content.ValidationErrors.Count == 0
            ? "Expected content validation to succeed"
            : string.Join(" | ", content.ValidationErrors);
    }
}
