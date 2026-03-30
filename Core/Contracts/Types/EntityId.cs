using System;

namespace Roguelike.Core;

public readonly struct EntityId : IEquatable<EntityId>
{
    public EntityId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static EntityId New() => new(Guid.NewGuid());

    public static EntityId From(string value) => new(Guid.Parse(value));

    public bool IsValid => Value != Guid.Empty;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value == Guid.Empty ? "invalid" : Value.ToString("N")[..8];

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

    public static readonly EntityId Invalid = new(Guid.Empty);
}
