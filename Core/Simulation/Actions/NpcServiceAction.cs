using System;
using System.Linq;

namespace Roguelike.Core;

public sealed class NpcServiceAction : IAction
{
    public NpcServiceAction(EntityId actorId, EntityId npcId, string serviceId)
    {
        ActorId = actorId;
        NpcId = npcId;
        ServiceId = serviceId;
    }

    public EntityId ActorId { get; }
    public EntityId NpcId { get; }
    public string ServiceId { get; }
    public ActionType Type => ActionType.UseItem;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var npc = world.GetEntity(NpcId);
        if (actor is null || actor != world.Player || actor.Faction != Faction.Player || actor.Stats.HP <= 0
            || npc is null || npc == actor || npc.Stats.HP <= 0 || npc.Faction != Faction.Neutral
            || Math.Abs(actor.Position.X - npc.Position.X) + Math.Abs(actor.Position.Y - npc.Position.Y) != 1
            || world is not WorldState state || ResolveService(state) is not { Cost: > 0, HealAmount: > 0 } service)
        {
            return ActionResult.Invalid;
        }

        return actor.Stats.HP < actor.Stats.MaxHP && actor.GetComponent<WalletComponent>() is { } wallet && wallet.Gold >= service.Cost
            ? ActionResult.Success
            : ActionResult.Blocked;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var npc = world.GetEntity(NpcId)!;
        var service = ResolveService(world)!;
        var healed = Math.Min(service.HealAmount, actor.Stats.MaxHP - actor.Stats.HP);
        actor.GetComponent<WalletComponent>()!.Gold -= service.Cost;
        actor.Stats.HP += healed;
        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, npc.Position },
            LogMessages = { $"{npc.Name} dresses {actor.Name}'s wounds, restoring {healed} HP for {service.Cost} gold." },
        };
    }

    public int GetEnergyCost() => 1000;

    private NpcServiceTemplate? ResolveService(WorldState world)
    {
        var npc = world.GetEntity(NpcId)?.GetComponent<NpcComponent>();
        return ServiceId == "field_dressing" && npc is not null && world.ContentDatabase is { } content
            && content.TryGetNpcTemplate(npc.TemplateId, out var template)
            ? template.Services?.FirstOrDefault(service => service.Id == ServiceId)
            : null;
    }
}
