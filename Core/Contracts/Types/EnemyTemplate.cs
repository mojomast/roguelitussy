namespace Roguelike.Core;

public sealed record EnemyTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    Stats BaseStats,
    string BrainType,
    Faction Faction,
    int MinDepth,
    int MaxDepth,
    int SpawnWeight,
    string? LootTableId);
