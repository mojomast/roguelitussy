using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class TurnScheduler : ITurnScheduler
{
    public int EnergyThreshold => 1000;

    private readonly List<IEntity> _entities = new();
    private readonly Queue<IEntity> _ready = new();

    public void Register(IEntity entity) => _entities.Add(entity);

    public void Unregister(EntityId id) => _entities.RemoveAll(e => e.Id == id);

    public void BeginRound(WorldState world)
    {
        _ready.Clear();
        foreach (var entity in _entities)
        {
            if (entity.IsAlive)
                entity.Stats.Energy += entity.Stats.Speed * 10;
        }
        EnqueueReady();
    }

    public bool HasNextActor() => _ready.Count > 0;

    public IEntity? GetNextActor() => _ready.Count > 0 ? _ready.Dequeue() : null;

    public void ConsumeEnergy(EntityId actorId, int cost)
    {
        var entity = _entities.Find(e => e.Id == actorId);
        if (entity != null)
        {
            entity.Stats.Energy -= cost;
            // Re-enqueue if still above threshold
            if (entity.IsAlive && entity.Stats.Energy >= EnergyThreshold)
                _ready.Enqueue(entity);
        }
    }

    public void EndRound(WorldState world)
    {
        world.TurnNumber++;
    }

    private void EnqueueReady()
    {
        // Sort: highest energy first, player wins ties
        var ready = _entities
            .Where(e => e.IsAlive && e.Stats.Energy >= EnergyThreshold)
            .OrderByDescending(e => e.Stats.Energy)
            .ThenByDescending(e => e.Faction == Faction.Player ? 1 : 0);

        foreach (var entity in ready)
            _ready.Enqueue(entity);
    }
}
