namespace Roguelike.Core;

public sealed class AttackAction : IAction
{
    public AttackAction(EntityId actorId, EntityId targetId)
    {
        ActorId = actorId;
        TargetId = targetId;
    }

    public EntityId ActorId { get; }

    public EntityId TargetId { get; }

    public ActionType Type => ActionType.MeleeAttack;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var target = world.GetEntity(TargetId);
        if (actor is null || target is null || !actor.IsAlive || !target.IsAlive)
        {
            return ActionResult.Invalid;
        }

        if (actor.Faction == target.Faction)
        {
            return ActionResult.Invalid;
        }

        return actor.Position.ChebyshevTo(target.Position) <= 1 ? ActionResult.Success : ActionResult.Blocked;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var target = world.GetEntity(TargetId)!;
        world.CombatResolver ??= new CombatResolver(world.Seed);

        ItemTemplate? weapon = null;
        string? weaponName = null;
        var inventory = actor.GetComponent<InventoryComponent>();
        if (inventory is not null)
        {
            var equipped = inventory.GetEquipped(EquipSlot.MainHand);
            if (equipped is not null && world.ContentDatabase is not null)
            {
                world.ContentDatabase.TryGetItemTemplate(equipped.Item.TemplateId, out var template);
                if (template is not null && template.Category == ItemCategory.Weapon)
                {
                    weapon = template;
                    weaponName = template.DisplayName;
                }
            }
        }

        var damage = world.CombatResolver.ResolveMeleeAttack(actor, target, world.TurnNumber, weapon);
        var statusEffectsApplied = new System.Collections.Generic.List<StatusEffectInstance>();
        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, target.Position },
        };

        if (damage.IsMiss)
        {
            outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, Type, new[] { damage }, System.Array.Empty<StatusEffectInstance>()));
            outcome.LogMessages.Add(weaponName is not null
                ? $"{actor.Name} swings {weaponName} at {target.Name} but misses."
                : $"{actor.Name} misses {target.Name}.");
            return outcome;
        }

        target.Stats.HP -= damage.FinalDamage;

        // Process on-hit effects before kill check
        if (!damage.IsKill && target.Stats.HP > 0 && weapon is not null)
        {
            var onHitApplied = world.CombatResolver.ProcessOnHitEffects(target, weapon);
            foreach (var effect in onHitApplied)
            {
                statusEffectsApplied.Add(effect);
                outcome.LogMessages.Add($"{target.Name} is afflicted with {effect.Type}!");
            }
        }

        outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, Type, new[] { damage }, statusEffectsApplied));

        if (damage.IsKill || target.Stats.HP <= 0)
        {
            target.Stats.HP = 0;

            var xpComponent = target.GetComponent<XpValueComponent>();
            if (xpComponent is not null)
            {
                var attackerProgression = actor.GetComponent<ProgressionComponent>();
                if (attackerProgression is not null)
                {
                    attackerProgression.Kills++;
                    var progressionResult = ProgressionService.AwardExperience(actor, xpComponent.Value);
                    if (progressionResult.ExperienceGained > 0)
                    {
                        outcome.LogMessages.Add($"{actor.Name} gains {progressionResult.ExperienceGained} XP.");
                    }

                    foreach (var level in progressionResult.ReachedLevels)
                    {
                        outcome.LogMessages.Add($"{actor.Name} reaches level {level}!");
                    }
                }
            }

            world.RemoveEntity(TargetId);
            outcome.LogMessages.Add(weaponName is not null
                ? $"{actor.Name} kills {target.Name} with {weaponName} for {damage.FinalDamage} damage."
                : $"{actor.Name} kills {target.Name} for {damage.FinalDamage} damage.");
            return outcome;
        }

        var criticalText = damage.IsCritical ? " critically" : string.Empty;
        outcome.LogMessages.Add(weaponName is not null
            ? $"{actor.Name}{criticalText} strikes {target.Name} with {weaponName} for {damage.FinalDamage} damage."
            : $"{actor.Name}{criticalText} hits {target.Name} for {damage.FinalDamage} damage.");
        return outcome;
    }

    public int GetEnergyCost() => 1000;
}