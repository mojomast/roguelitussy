using Roguelike.Core.Simulation;

namespace Roguelike.Core.AI.Brains;

public sealed class RangedKiterBrain : IBrain
{
    private const int KiteDistance = 2;

    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        var player = world.Player;
        if (player == null || !player.IsAlive)
            return new WaitAction(self.Id);

        int dist = self.Position.DistanceTo(player.Position);

        // Too close → flee away from player
        if (dist <= KiteDistance)
        {
            var fleePos = GetFleePosition(self, player, world);
            if (fleePos != self.Position)
                return new MoveAction(self.Id, fleePos);
        }

        // Within view range → hold and "shoot" (wait as placeholder for ranged attack)
        if (dist <= self.Stats.ViewRadius)
            return new WaitAction(self.Id);

        // Out of range → wait
        return new WaitAction(self.Id);
    }

    private static Position GetFleePosition(IEntity self, IEntity threat, IWorldState world)
    {
        int bestDist = self.Position.DistanceTo(threat.Position);
        var bestPos = self.Position;

        foreach (var dir in Position.Cardinals)
        {
            var candidate = self.Position + dir;
            if (!world.IsWalkable(candidate))
                continue;

            int d = candidate.DistanceTo(threat.Position);
            if (d > bestDist)
            {
                bestDist = d;
                bestPos = candidate;
            }
        }

        return bestPos;
    }
}
