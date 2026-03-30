using System.Collections.Generic;

namespace Roguelike.Core;

public interface IAction
{
    EntityId ActorId { get; }

    ActionType Type { get; }

    ActionResult Validate(IWorldState world);

    ActionOutcome Execute(WorldState world);

    int GetEnergyCost();
}

public sealed class ActionOutcome
{
    public ActionResult Result { get; init; }

    public List<CombatEvent> CombatEvents { get; init; } = new();

    public List<string> LogMessages { get; init; } = new();

    public List<Position> DirtyPositions { get; init; } = new();

    public static ActionOutcome Fail(ActionResult reason) => new() { Result = reason };

    public static ActionOutcome Ok() => new() { Result = ActionResult.Success };
}
