using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubBrain : IBrain
{
    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder) => new StubAction(self.Id);
}
