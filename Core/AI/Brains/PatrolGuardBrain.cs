using Roguelike.Core.Simulation;

namespace Roguelike.Core.AI.Brains;

public sealed class LastKnownPosition
{
    public Position Position { get; set; }
    public bool HasValue { get; set; }
}

public sealed class PatrolGuardBrain : IBrain
{
    private readonly CombatResolver _resolver;

    public PatrolGuardBrain(CombatResolver resolver)
    {
        _resolver = resolver;
    }

    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        var player = world.Player;
        if (player == null || !player.IsAlive)
            return new WaitAction(self.Id);

        bool canSeePlayer = self.Position.DistanceTo(player.Position) <= self.Stats.ViewRadius;

        if (canSeePlayer)
        {
            // Update last known position
            var lkp = GetOrCreateLKP(self);
            lkp.Position = player.Position;
            lkp.HasValue = true;

            // Adjacent → attack
            if (self.Position.ChebyshevTo(player.Position) == 1)
                return new AttackAction(self.Id, player.Id, _resolver);

            // Chase
            var path = pathfinder.FindPath(self.Position, player.Position, world);
            if (path.Count > 0)
                return new MoveAction(self.Id, path[0]);
        }

        // Not visible — move toward last known position
        var lastKnown = self.GetComponent<LastKnownPosition>();
        if (lastKnown is { HasValue: true })
        {
            if (self.Position == lastKnown.Position)
            {
                // Arrived at last known position, clear it
                lastKnown.HasValue = false;
            }
            else
            {
                var path = pathfinder.FindPath(self.Position, lastKnown.Position, world);
                if (path.Count > 0)
                    return new MoveAction(self.Id, path[0]);

                // Can't path there, give up
                lastKnown.HasValue = false;
            }
        }

        // Patrol: random cardinal move or wait
        foreach (var dir in Position.Cardinals)
        {
            var candidate = self.Position + dir;
            if (world.IsWalkable(candidate))
                return new MoveAction(self.Id, candidate);
        }

        return new WaitAction(self.Id);
    }

    private static LastKnownPosition GetOrCreateLKP(IEntity self)
    {
        var lkp = self.GetComponent<LastKnownPosition>();
        if (lkp == null)
        {
            lkp = new LastKnownPosition();
            self.SetComponent(lkp);
        }
        return lkp;
    }
}
