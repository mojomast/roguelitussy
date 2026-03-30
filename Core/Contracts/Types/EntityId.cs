using System;

namespace Roguelike.Core;

public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly Guid Value;

    public EntityId(Guid value) => Value = value;

    public static EntityId New() => new(Guid.NewGuid());
    public static EntityId From(string s) => new(Guid.Parse(s));

    public bool IsValid => Value != Guid.Empty;
    public static readonly EntityId Invalid = new(Guid.Empty);

    public bool Equals(EntityId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityId id && Equals(id);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString()[..8];

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
}
