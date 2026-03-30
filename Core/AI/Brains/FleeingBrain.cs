using Roguelike.Core.Simulation;

namespace Roguelike.Core.AI.Brains;

public sealed class FleeingBrain : IBrain
{
    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        var player = world.Player;
        if (player == null || !player.IsAlive)
            return new WaitAction(self.Id);

        // Pick the cardinal direction that maximizes distance from player
        int bestDist = self.Position.DistanceTo(player.Position);
        Position bestPos = self.Position;

        foreach (var dir in Position.Cardinals)
        {
            var candidate = self.Position + dir;
            if (!world.IsWalkable(candidate))
                continue;

            int d = candidate.DistanceTo(player.Position);
            if (d > bestDist)
            {
                bestDist = d;
                bestPos = candidate;
            }
        }

        if (bestPos != self.Position)
            return new MoveAction(self.Id, bestPos);

        // Cornered → wait
        return new WaitAction(self.Id);
    }
}
