namespace Roguelike.Core;

public interface ITurnScheduler
{
    int EnergyThreshold { get; }

    void BeginRound(WorldState world);

    bool HasNextActor();

    IEntity? GetNextActor();

    void ConsumeEnergy(EntityId actorId, int cost);

    void EndRound(WorldState world);

    void Register(IEntity entity);

    void Unregister(EntityId id);
}
