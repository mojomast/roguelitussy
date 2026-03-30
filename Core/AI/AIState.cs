namespace Roguelike.Core;

public enum AIState : byte
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Flee,
}

public sealed class AIStateComponent
{
    public AIState State { get; set; } = AIState.Idle;

    public int IdleTurns { get; set; }

    public Position PatrolTarget { get; set; } = Position.Invalid;

    public int PatrolSteps { get; set; }

    public int PatrolSequence { get; set; }

    public Position LastKnownTargetPosition { get; set; } = Position.Invalid;

    public EntityId TargetId { get; set; } = EntityId.Invalid;
}