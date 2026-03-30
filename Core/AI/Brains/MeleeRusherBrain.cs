using Roguelike.Core.Simulation;

namespace Roguelike.Core.AI.Brains;

public sealed class MeleeRusherBrain : IBrain
{
    private readonly CombatResolver _resolver;

    public MeleeRusherBrain(CombatResolver resolver)
    {
        _resolver = resolver;
    }

    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        var player = world.Player;
        if (player == null || !player.IsAlive)
            return new WaitAction(self.Id);

        // Adjacent to player → attack
        if (self.Position.ChebyshevTo(player.Position) == 1)
            return new AttackAction(self.Id, player.Id, _resolver);

        // Can see player → pathfind toward them
        if (self.Position.DistanceTo(player.Position) <= self.Stats.ViewRadius)
        {
            var path = pathfinder.FindPath(self.Position, player.Position, world);
            if (path.Count > 0)
                return new MoveAction(self.Id, path[0]);
        }

        // Otherwise → wait
        return new WaitAction(self.Id);
    }
}
