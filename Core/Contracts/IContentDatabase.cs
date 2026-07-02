using System.Collections.Generic;

namespace Roguelike.Core;

public interface IContentDatabase
{
    int ContentVersion { get; }

    string ContentHash { get; }

    IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; }

    IReadOnlyDictionary<string, PerkTemplate> PerkTemplates { get; }

    IReadOnlyDictionary<string, NpcTemplate> NpcTemplates { get; }

    IReadOnlyDictionary<string, DialogueTemplate> DialogueTemplates { get; }

    IReadOnlyDictionary<string, StatusEffectDefinition> StatusEffects { get; }

    IReadOnlyDictionary<string, TrapTemplate> TrapTemplates { get; }

    IReadOnlyDictionary<string, RelicTemplate> RelicTemplates { get; }

    IReadOnlyDictionary<string, FloorEventDefinition> FloorEvents { get; }

    IReadOnlyDictionary<string, SynergyDefinition> Synergies { get; }

    IReadOnlyDictionary<string, AscensionModifier> AscensionModifiers { get; }

    IReadOnlyDictionary<string, DailyModifierDefinition> DailyModifiers { get; }

    IReadOnlyDictionary<string, NarrativeTemplate> NarrativeTemplates { get; }

    IReadOnlyDictionary<string, FactionDefinition> Factions { get; }

    bool TryGetItemTemplate(string templateId, out ItemTemplate template);

    bool TryGetEnemyTemplate(string templateId, out EnemyTemplate template);

    bool TryGetAbilityTemplate(string abilityId, out AbilityTemplate template);

    bool TryGetPerkTemplate(string perkId, out PerkTemplate template);

    bool TryGetNpcTemplate(string templateId, out NpcTemplate template);

    bool TryGetDialogueTemplate(string dialogueId, out DialogueTemplate template);

    bool TryGetStatusEffect(string statusId, out StatusEffectDefinition definition);

    bool TryGetTrapTemplate(string templateId, out TrapTemplate template);

    bool TryGetRelicTemplate(string relicId, out RelicTemplate template);

    bool TryGetFloorEvent(string eventId, out FloorEventDefinition definition);

    bool TryGetSynergy(string synergyId, out SynergyDefinition definition);

    bool TryGetAscensionModifier(string modifierId, out AscensionModifier modifier);

    bool TryGetDailyModifier(string modifierId, out DailyModifierDefinition modifier);

    bool TryGetNarrativeTemplate(string templateId, out NarrativeTemplate template);

    bool TryGetFaction(string factionId, out FactionDefinition faction);

    IReadOnlyList<ItemTemplate> GetAvailableItems(int depth);

    IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth);

    IReadOnlyList<NpcTemplate> GetAvailableNpcs(int depth);
}
