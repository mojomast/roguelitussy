namespace Roguelike.Core;

public interface IBrain
{
    IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder);
}
