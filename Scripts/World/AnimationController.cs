using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public enum AnimationType
{
    Move,
    Attack,
    Damage,
    Death,
}

public readonly record struct AnimationRecord(AnimationType Type, EntityId EntityId, Vector2 From, Vector2 To);

public sealed class AnimationController
{
    private readonly List<AnimationRecord> _history = new();

    public IReadOnlyList<AnimationRecord> History => _history;

    public void AnimateMove(EntityId entityId, Node2D sprite, Vector2 targetPosition)
    {
        var startPosition = sprite.Position;
        sprite.Position = targetPosition;
        _history.Add(new AnimationRecord(AnimationType.Move, entityId, startPosition, targetPosition));
    }

    public void AnimateAttack(EntityId entityId, Node2D sprite, Vector2 targetPosition)
    {
        var startPosition = sprite.Position;
        var bump = startPosition + ((targetPosition - startPosition) * 0.3f);
        sprite.Position = startPosition;
        _history.Add(new AnimationRecord(AnimationType.Attack, entityId, startPosition, bump));
    }

    public void AnimateDamage(EntityId entityId, Node2D sprite)
    {
        sprite.Modulate = Colors.Red;
        sprite.Modulate = Colors.White;
        _history.Add(new AnimationRecord(AnimationType.Damage, entityId, sprite.Position, sprite.Position));
    }

    public void AnimateDeath(EntityId entityId, Node2D sprite)
    {
        sprite.Visible = false;
        sprite.Modulate = Colors.Transparent;
        _history.Add(new AnimationRecord(AnimationType.Death, entityId, sprite.Position, sprite.Position));
    }

    public void ClearHistory()
    {
        _history.Clear();
    }
}