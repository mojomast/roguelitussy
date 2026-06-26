namespace Roguelike.Core;

public interface ITurnScheduler
{
    int EnergyThreshold { get; }

    void BeginRound(WorldState world);

    bool HasNextActor();

    IEntity? GetNextActor();

    StatusTickResult? ConsumeEnergy(EntityId actorId, int cost);

    void EndRound(WorldState world);

    void Register(IEntity entity);

    void Unregister(EntityId id);

    int GetOrder(EntityId actorId);

    int NextOrder { get; set; }

    void AttachWorld(WorldState world);
}
