using System.Collections.Generic;

namespace Roguelike.Core;

public interface IContentDatabase
{
    IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; }

    IReadOnlyDictionary<string, PerkTemplate> PerkTemplates { get; }

    IReadOnlyDictionary<string, NpcTemplate> NpcTemplates { get; }

    IReadOnlyDictionary<string, DialogueTemplate> DialogueTemplates { get; }

    bool TryGetItemTemplate(string templateId, out ItemTemplate template);

    bool TryGetEnemyTemplate(string templateId, out EnemyTemplate template);

    bool TryGetAbilityTemplate(string abilityId, out AbilityTemplate template);

    bool TryGetPerkTemplate(string perkId, out PerkTemplate template);

    bool TryGetNpcTemplate(string templateId, out NpcTemplate template);

    bool TryGetDialogueTemplate(string dialogueId, out DialogueTemplate template);

    IReadOnlyList<ItemTemplate> GetAvailableItems(int depth);

    IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth);

    IReadOnlyList<NpcTemplate> GetAvailableNpcs(int depth);
}
