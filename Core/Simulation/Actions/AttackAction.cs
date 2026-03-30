using System;
using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class AttackAction : IAction
{
    public EntityId ActorId { get; }
    public EntityId TargetId { get; }
    public ActionType Type => ActionType.MeleeAttack;

    private readonly CombatResolver _resolver;

    public AttackAction(EntityId actorId, EntityId targetId, CombatResolver resolver)
    {
        ActorId = actorId;
        TargetId = targetId;
        _resolver = resolver;
    }

    public int GetEnergyCost() => 1000;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var target = world.GetEntity(TargetId);
        if (actor == null || target == null || !actor.IsAlive || !target.IsAlive)
            return ActionResult.Invalid;

        if (actor.Position.ChebyshevTo(target.Position) != 1)
            return ActionResult.Invalid;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var target = world.GetEntity(TargetId);
        if (actor == null || target == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        var dmg = _resolver.ResolveAttack(actor, target, world);

        var messages = new List<string>();
        if (dmg.IsMiss)
            messages.Add($"{actor.Name} misses {target.Name}.");
        else if (dmg.IsKill)
            messages.Add($"{actor.Name} kills {target.Name}! ({dmg.FinalDamage} dmg{(dmg.IsCritical ? ", CRIT" : "")})");
        else
            messages.Add($"{actor.Name} hits {target.Name} for {dmg.FinalDamage} damage.{(dmg.IsCritical ? " Critical!" : "")}");

        if (dmg.IsKill)
            world.RemoveEntity(target.Id);

        var combatEvent = new CombatEvent(
            world.TurnNumber,
            ActionType.MeleeAttack,
            new List<DamageResult> { dmg },
            Array.Empty<StatusEffectInstance>()
        );

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            CombatEvents = new List<CombatEvent> { combatEvent },
            LogMessages = messages,
            DirtyPositions = new List<Position> { actor.Position, target.Position }
        };
    }
}
