using System.Collections.Generic;

namespace Roguelike.Core;

public interface IContentDatabase
{
    IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; }

    bool TryGetItemTemplate(string templateId, out ItemTemplate template);

    bool TryGetEnemyTemplate(string templateId, out EnemyTemplate template);

    bool TryGetAbilityTemplate(string abilityId, out AbilityTemplate template);

    IReadOnlyList<ItemTemplate> GetAvailableItems(int depth);

    IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth);
}
