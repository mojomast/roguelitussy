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
                var actorId = ready[0];
                var actor = _currentWorld.GetEntity(actorId);
                if (actor is null || !actor.IsAlive)
                {
                    _actors.Remove(actorId);
                    continue;
                }

                return actor;
            }

            AdvanceTime(_currentWorld);
        }

        return null;
    }

    public StatusTickResult? ConsumeEnergy(EntityId actorId, int cost)
    {
        if (!_actors.TryGetValue(actorId, out var actor))
        {
            return null;
        }

        actor.Energy -= Math.Max(0, cost);
        if (_currentWorld?.GetEntity(actorId) is { } entity)
        {
            entity.Stats.Energy = actor.Energy;
            var tickResult = _currentWorld.ContentDatabase is { } db
                ? StatusEffectProcessor.Tick(_currentWorld, actorId, db)
                : StatusEffectProcessor.Tick(_currentWorld, actorId);
            if (tickResult.Died)
            {
                _actors.Remove(actorId);
                return tickResult;
            }

            actor.BaseSpeed = entity.Stats.Speed;
            return tickResult;
        }

        return null;
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

        var order = _nextOrder;
        if (_currentWorld?.SchedulerOrders.TryGetValue(entity.Id, out var savedOrder) == true)
        {
            order = savedOrder;
            _currentWorld.SchedulerOrders.Remove(entity.Id);
        }
        else
        {
            _nextOrder++;
        }

        _actors[entity.Id] = new ActorState(entity.Stats.Energy, order, entity.Stats.Speed);
    }

    public int GetOrder(EntityId actorId) => _actors.TryGetValue(actorId, out var actor) ? actor.Order : 0;

    public int NextOrder
    {
        get => _nextOrder;
        set => _nextOrder = value;
    }

    public void AttachWorld(WorldState world) => _currentWorld = world;

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

            var speed = Math.Max(1, world.ContentDatabase is { } db
                ? StatusEffectProcessor.GetEffectiveSpeed(entity, db)
                : StatusEffectProcessor.GetEffectiveSpeed(entity));
            pair.Value.BaseSpeed = entity.Stats.Speed;
            pair.Value.Energy += (BaseEnergyGain * speed) / 100;
            entity.Stats.Energy = pair.Value.Energy;
        }
    }

    private void RegisterMissing(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.IsAlive && !_actors.ContainsKey(entity.Id) && ShouldScheduleEntity(entity))
            {
                Register(entity);
            }
        }
    }

    private static bool ShouldScheduleEntity(IEntity entity)
    {
        return entity.Faction != Faction.Neutral || entity.GetComponent<IBrain>() is not null;
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
