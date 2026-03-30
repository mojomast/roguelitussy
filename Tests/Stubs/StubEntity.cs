using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubEntity : IEntity
{
    private readonly Dictionary<System.Type, object> _components = new();

    public StubEntity(
        string name,
        Position position,
        Faction faction = Faction.Enemy,
        bool blocksMovement = true,
        bool blocksSight = false,
        Stats? stats = null,
        EntityId? id = null)
    {
        Name = name;
        Position = position;
        Faction = faction;
        BlocksMovement = blocksMovement;
        BlocksSight = blocksSight;
        Stats = stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 };
        Id = id ?? EntityId.New();
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
