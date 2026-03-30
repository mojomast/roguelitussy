namespace Roguelike.Core;

public interface IEntity
{
    EntityId Id { get; }
    string Name { get; }
    Position Position { get; set; }
    Stats Stats { get; }
    Faction Faction { get; }
    bool BlocksMovement { get; }
    bool BlocksSight { get; }
    bool IsAlive { get; }

    bool HasComponent<T>() where T : class;
    T? GetComponent<T>() where T : class;
    void SetComponent<T>(T component) where T : class;
    void RemoveComponent<T>() where T : class;
}
