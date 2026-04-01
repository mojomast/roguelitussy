using System.Linq;
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
        registry.Add("Content.Depth filters stay stable", DepthFiltersStayStable);
    }

    private static void LoadsAndValidatesAllJson()
    {
        var content = LoadContent();

        Expect.True(content.IsValid, FormatErrors(content));
        Expect.Equal(22, content.ItemDefinitions.Count, "Expected the full item set to load");
        Expect.Equal(13, content.EnemyDefinitions.Count, "Expected the full enemy set to load");
        Expect.Equal(8, content.AbilityDefinitions.Count, "Expected the full ability set to load");
        Expect.Equal(4, content.PerkDefinitions.Count, "Expected the initial perk set to load");
        Expect.Equal(2, content.DialogueDefinitions.Count, "Expected the full dialog set to load");
        Expect.Equal(2, content.NpcDefinitions.Count, "Expected the full NPC set to load");
        Expect.Equal(9, content.StatusEffects.Count, "Expected the full status effect set to load");
        Expect.True(content.RoomPrefabs.Count >= 10, "Expected at least the baseline room prefab set to load");
        Expect.Equal(15, content.LootTables.Count, "Expected the full loot table set to load");
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

        Expect.True(content.TryGetNpcTemplate("quartermaster_vale", out var vendor), "Quartermaster Vale should project into the NPC surface");
        Expect.True(vendor.IsMerchant, "Quartermaster Vale should project merchant stock");
        Expect.Equal("merchant_intro", vendor.DialogueId, "NPC dialog ids should survive projection");

        Expect.True(content.TryGetPerkTemplate("quartermasters_eye", out var perk), "Quartermaster's Eye should project into the perk surface");
        Expect.Equal(2, perk.UnlockLevel, "Perk unlock levels should survive projection");

        Expect.True(content.TryGetDialogueTemplate("merchant_intro", out var dialog), "Merchant intro dialog should project into the dialogue surface");
        Expect.True(dialog.Nodes.ContainsKey(dialog.StartNodeId), "Dialog templates should retain their start node");
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

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        return content;
    }

    private static string FormatErrors(ContentLoader content)
    {
        return content.ValidationErrors.Count == 0
            ? "Expected content validation to succeed"
            : string.Join(" | ", content.ValidationErrors);
    }
}