using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class TurnScheduler : ITurnScheduler
{
    private const int BaseEnergyGain = 100;

    private sealed class ActorState
    {
        public ActorState(int energy, int order, int baseSpeed)
        {
            Energy = energy;
            Order = order;
            BaseSpeed = baseSpeed;
        }

        public int Energy { get; set; }

        public int Order { get; }

        public int BaseSpeed { get; set; }
    }

    private readonly Dictionary<EntityId, ActorState> _actors = new();
    private int _nextOrder;
    private WorldState? _currentWorld;

    public int EnergyThreshold => 1000;

    public void BeginRound(WorldState world)
    {
        _currentWorld = world;
        world.TurnNumber++;
        RegisterMissing(world);
        PruneMissing(world);
    }

    public bool HasNextActor()
    {
        if (_currentWorld is not null)
        {
            PruneMissing(_currentWorld);
        }

        return _actors.Count > 0;
    }

    public IEntity? GetNextActor()
    {
        if (_currentWorld is null)
        {
            return null;
        }

        while (_actors.Count > 0)
        {
            PruneMissing(_currentWorld);
            var ready = _actors
                .OrderByDescending(pair => pair.Value.Energy)
                .ThenBy(pair => pair.Value.Order)
                .Where(pair => pair.Value.Energy >= EnergyThreshold)
                .Select(pair => pair.Key)
                .ToArray();

            if (ready.Length > 0)
            {
                var actor = _currentWorld.GetEntity(ready[0]);
                if (actor is not null && actor.IsAlive)
                {
                    return actor;
                }

                _actors.Remove(ready[0]);
                continue;
            }

            AdvanceTime(_currentWorld);
        }

        return null;
    }

    public void ConsumeEnergy(EntityId actorId, int cost)
    {
        if (!_actors.TryGetValue(actorId, out var actor))
        {
            return;
        }

        actor.Energy -= Math.Max(0, cost);
        if (_currentWorld?.GetEntity(actorId) is { } entity)
        {
            entity.Stats.Energy = actor.Energy;
            var tickResult = StatusEffectProcessor.Tick(_currentWorld, actorId);
            if (tickResult.Died)
            {
                _actors.Remove(actorId);
                return;
            }

            actor.BaseSpeed = entity.Stats.Speed;
        }
    }

    public void EndRound(WorldState world)
    {
        PruneMissing(world);
        _currentWorld = null;
    }

    public void Register(IEntity entity)
    {
        if (_actors.TryGetValue(entity.Id, out var existing))
        {
            existing.BaseSpeed = entity.Stats.Speed;
            existing.Energy = entity.Stats.Energy;
            return;
        }

        _actors[entity.Id] = new ActorState(entity.Stats.Energy, _nextOrder++, entity.Stats.Speed);
    }

    public void Unregister(EntityId id)
    {
        _actors.Remove(id);
    }

    public int GetEnergy(EntityId actorId) => _actors.TryGetValue(actorId, out var actor) ? actor.Energy : 0;

    private void AdvanceTime(WorldState world)
    {
        foreach (var pair in _actors.OrderBy(pair => pair.Value.Order).ToArray())
        {
            var entity = world.GetEntity(pair.Key);
            if (entity is null || !entity.IsAlive)
            {
                _actors.Remove(pair.Key);
                continue;
            }

            var speed = Math.Max(1, StatusEffectProcessor.GetEffectiveSpeed(entity));
            pair.Value.BaseSpeed = entity.Stats.Speed;
            pair.Value.Energy += (BaseEnergyGain * speed) / 100;
            entity.Stats.Energy = pair.Value.Energy;
        }
    }

    private void RegisterMissing(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.IsAlive && !_actors.ContainsKey(entity.Id))
            {
                Register(entity);
            }
        }
    }

    private void PruneMissing(WorldState world)
    {
        var aliveIds = world.Entities.Where(entity => entity.IsAlive).Select(entity => entity.Id).ToHashSet();

        foreach (var actorId in _actors.Keys.Where(id => !aliveIds.Contains(id)).ToArray())
        {
            _actors.Remove(actorId);
        }
    }
}