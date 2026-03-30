using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class Entity : IEntity
{
    private readonly Dictionary<Type, object> _components = new();

    public EntityId Id { get; }
    public string Name { get; set; }
    public Position Position { get; set; }
    public Stats Stats { get; }
    public Faction Faction { get; set; }
    public bool BlocksMovement { get; set; } = true;
    public bool BlocksSight { get; set; }
    public bool IsAlive => Stats.IsAlive;

    public Entity(EntityId id, string name, Position position, Stats stats, Faction faction)
    {
        Id = id;
        Name = name;
        Position = position;
        Stats = stats;
        Faction = faction;
    }

    public bool HasComponent<T>() where T : class => _components.ContainsKey(typeof(T));

    public T? GetComponent<T>() where T : class =>
        _components.TryGetValue(typeof(T), out var c) ? (T)c : null;

    public void SetComponent<T>(T component) where T : class =>
        _components[typeof(T)] = component;

    public void RemoveComponent<T>() where T : class =>
        _components.Remove(typeof(T));
}
