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
    string? LootTableId,
    int GoldMin,
    int GoldMax,
    int XpValue,
    AIParameters AIParameters,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<BossPhaseTemplate>? BossPhases = null);

public sealed record BossPhaseTemplate(
    int Phase,
    double Threshold,
    string AbilityId,
    int StatBoost,
    string StatusEffect,
    string Message);
