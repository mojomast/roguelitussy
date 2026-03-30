using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class Entity : IEntity
{
    private readonly Dictionary<Type, object> _components = new();

    public Entity(
        string name,
        Position position,
        Stats stats,
        Faction faction,
        bool blocksMovement = true,
        bool blocksSight = false,
        EntityId? id = null)
    {
        Id = id ?? EntityId.New();
        Name = name;
        Position = position;
        Stats = stats;
        Faction = faction;
        BlocksMovement = blocksMovement;
        BlocksSight = blocksSight;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public Position Position { get; set; }

    public Stats Stats { get; }

    public Faction Faction { get; }

    public bool BlocksMovement { get; }

    public bool BlocksSight { get; }

    public bool IsAlive => Stats.IsAlive;

    public bool HasComponent<T>() where T : class => _components.ContainsKey(typeof(T));

    public T? GetComponent<T>() where T : class => _components.TryGetValue(typeof(T), out var value) ? (T)value : null;

    public void SetComponent<T>(T component) where T : class => _components[typeof(T)] = component;

    public void RemoveComponent<T>() where T : class => _components.Remove(typeof(T));
}