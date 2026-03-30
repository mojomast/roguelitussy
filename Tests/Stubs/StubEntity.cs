using System;
using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubEntity : IEntity
{
    public EntityId Id { get; init; }
    public string Name { get; set; } = "Stub";
    public Position Position { get; set; }
    public Stats Stats { get; init; } = new();
    public Faction Faction { get; init; }
    public bool BlocksMovement { get; init; } = true;
    public bool BlocksSight { get; init; } = false;
    public bool IsAlive => Stats.IsAlive;

    private readonly Dictionary<Type, object> _components = new();

    public bool HasComponent<T>() where T : class =>
        _components.ContainsKey(typeof(T));

    public T? GetComponent<T>() where T : class =>
        _components.TryGetValue(typeof(T), out var c) ? (T)c : null;

    public void SetComponent<T>(T component) where T : class =>
        _components[typeof(T)] = component;

    public void RemoveComponent<T>() where T : class =>
        _components.Remove(typeof(T));
}
