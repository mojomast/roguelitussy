using System;

namespace Roguelike.Core;

public sealed class RangedAttackAction : IAction
{
    public const int MinimumRange = 3;
    public const int MaximumRange = 5;
    public const string ArrowTemplateId = "item_arrows_bundle";

    public RangedAttackAction(EntityId actorId, EntityId targetId)
    {
        ActorId = actorId;
        TargetId = targetId;
    }

    public EntityId ActorId { get; }

    public EntityId TargetId { get; }

    public ActionType Type => ActionType.RangedAttack;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var target = world.GetEntity(TargetId);
        if (actor is null || target is null || !actor.IsAlive || !target.IsAlive)
        {
            return ActionResult.Invalid;
        }

        if (actor.Faction == target.Faction || actor.Faction == Faction.Neutral || target.Faction == Faction.Neutral)
        {
            return ActionResult.Invalid;
        }

        var range = actor.Position.ChebyshevTo(target.Position);
        if (range < MinimumRange || range > MaximumRange)
        {
            return ActionResult.Blocked;
        }

        var inventory = actor.GetComponent<InventoryComponent>();
        if (inventory is null || !HasArrow(inventory))
        {
            return ActionResult.Invalid;
        }

        if (world is WorldState state)
        {
            if (!state.IsVisible(target.Position) || !HasLineOfSight(state, actor.Position, target.Position))
            {
                return ActionResult.Blocked;
            }
        }

        return ActionResult.Success;
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
        var inventory = actor.GetComponent<InventoryComponent>()!;
        inventory.TryConsumeOne(ArrowTemplateId, out _);

        world.CombatResolver ??= new CombatResolver(world.Seed);
        var damage = world.CombatResolver.ResolveMeleeAttack(actor, target, world.TurnNumber);
        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, target.Position },
        };

        if (damage.IsMiss)
        {
            outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, Type, new[] { damage }, Array.Empty<StatusEffectInstance>(), TargetId));
            outcome.LogMessages.Add($"{actor.Name} fires at {target.Name} but misses.");
            return outcome;
        }

        target.Stats.HP -= damage.FinalDamage;
        outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, Type, new[] { damage }, Array.Empty<StatusEffectInstance>(), TargetId));

        if (damage.IsKill || target.Stats.HP <= 0)
        {
            var death = DeathResolver.ResolveKill(world, actor, target);
            DeathResolver.AppendProgressionLogMessages(outcome.LogMessages, actor.Name, death);
            outcome.LogMessages.Add($"{actor.Name} shoots down {target.Name} for {damage.FinalDamage} damage.");
            DeathResolver.AppendLootLogMessages(outcome.LogMessages, death);
            return outcome;
        }

        var criticalText = damage.IsCritical ? " critically" : string.Empty;
        outcome.LogMessages.Add($"{actor.Name}{criticalText} shoots {target.Name} for {damage.FinalDamage} damage.");
        return outcome;
    }

    public int GetEnergyCost() => 1000;

    private static bool HasArrow(InventoryComponent inventory)
    {
        foreach (var item in inventory.Items)
        {
            if (string.Equals(item.TemplateId, ArrowTemplateId, StringComparison.OrdinalIgnoreCase) && item.StackCount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLineOfSight(WorldState world, Position from, Position to)
    {
        var x0 = from.X;
        var y0 = from.Y;
        var x1 = to.X;
        var y1 = to.Y;
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx - dy;

        while (x0 != x1 || y0 != y1)
        {
            var twiceError = error * 2;
            if (twiceError > -dy)
            {
                error -= dy;
                x0 += sx;
            }

            if (twiceError < dx)
            {
                error += dx;
                y0 += sy;
            }

            var position = new Position(x0, y0);
            if (position != to && world.BlocksSight(position))
            {
                return false;
            }
        }

        return true;
    }
}
