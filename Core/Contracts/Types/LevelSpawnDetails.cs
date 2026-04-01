namespace Roguelike.Core;

public sealed record EnemySpawnData(
    Position Position,
    string? TemplateId = null,
    bool IsBoss = false);

public sealed record ItemSpawnData(
    Position Position,
    string? TemplateId = null,
    int QualityBonus = 0);

public sealed record ChestSpawnData(
    Position Position,
    string? LootTableId = null);

public sealed record NpcSpawnData(
    Position Position,
    string TemplateId);