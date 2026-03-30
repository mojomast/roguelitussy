using System.Collections.Generic;

namespace Roguelike.Core;

public interface IContentDatabase
{
    ItemTemplate? GetItem(string templateId);
    IReadOnlyList<ItemTemplate> AllItems { get; }
    EnemyTemplate? GetEnemy(string templateId);
    IReadOnlyList<EnemyTemplate> AllEnemies { get; }
    IReadOnlyList<EnemyTemplate> GetEnemiesForDepth(int depth);
    IReadOnlyList<ItemTemplate> GetItemsForDepth(int depth);
}
