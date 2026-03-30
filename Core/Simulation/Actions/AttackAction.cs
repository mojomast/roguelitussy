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

        var damage = world.CombatResolver.ResolveMeleeAttack(actor, target, world.TurnNumber);
        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            CombatEvents =
            {
                new CombatEvent(world.TurnNumber, Type, new[] { damage }, System.Array.Empty<StatusEffectInstance>()),
            },
            DirtyPositions = { actor.Position, target.Position },
        };

        if (damage.IsMiss)
        {
            outcome.LogMessages.Add($"{actor.Name} misses {target.Name}.");
            return outcome;
        }

        target.Stats.HP -= damage.FinalDamage;
        if (damage.IsKill || target.Stats.HP <= 0)
        {
            target.Stats.HP = 0;
            world.RemoveEntity(TargetId);
            outcome.LogMessages.Add($"{actor.Name} kills {target.Name} for {damage.FinalDamage} damage.");
            return outcome;
        }

        var criticalText = damage.IsCritical ? " critically" : string.Empty;
        outcome.LogMessages.Add($"{actor.Name}{criticalText} hits {target.Name} for {damage.FinalDamage} damage.");
        return outcome;
    }

    public int GetEnergyCost() => 1000;
}