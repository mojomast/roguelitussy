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
    private sealed class MoveAnimationState
    {
        public required Node2D Sprite { get; init; }

        public required Vector2 From { get; init; }

        public required Vector2 To { get; init; }

        public float ElapsedSeconds { get; set; }

        public float DurationSeconds { get; init; }
    }

    private const float MoveDurationSeconds = 0.12f;
    private readonly List<AnimationRecord> _history = new();
    private readonly Dictionary<EntityId, MoveAnimationState> _activeMoves = new();

    public IReadOnlyList<AnimationRecord> History => _history;

    public int ActiveMoveCount => _activeMoves.Count;

    public bool IsMoveAnimating(EntityId entityId) => _activeMoves.ContainsKey(entityId);

    public void AnimateMove(EntityId entityId, Node2D sprite, Vector2 targetPosition)
    {
        var startPosition = sprite.Position;
        if (startPosition.Equals(targetPosition))
        {
            sprite.Position = targetPosition;
            _activeMoves.Remove(entityId);
            _history.Add(new AnimationRecord(AnimationType.Move, entityId, startPosition, targetPosition));
            return;
        }

        _activeMoves[entityId] = new MoveAnimationState
        {
            Sprite = sprite,
            From = startPosition,
            To = targetPosition,
            ElapsedSeconds = 0f,
            DurationSeconds = MoveDurationSeconds,
        };
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

    public void SpawnDamagePopup(Node parent, Vector2 position, int amount, bool isCrit, bool isHeal, bool isMiss = false)
    {
        var popup = new DamagePopup();
        popup.Position = position;
        popup.Setup(amount, isCrit, isHeal, isMiss);
        parent.AddChild(popup);
    }

    public void ClearHistory()
    {
        _history.Clear();
    }

    public void Advance(double delta)
    {
        if (_activeMoves.Count == 0)
        {
            return;
        }

        foreach (var entry in _activeMoves.ToArray())
        {
            var state = entry.Value;
            state.ElapsedSeconds += (float)delta;
            var progress = Math.Clamp(state.ElapsedSeconds / state.DurationSeconds, 0f, 1f);
            var eased = EaseOutCubic(progress);
            state.Sprite.Position = Lerp(state.From, state.To, eased);

            if (progress >= 1f)
            {
                state.Sprite.Position = state.To;
                _activeMoves.Remove(entry.Key);
            }
        }
    }

    public void CompleteAll()
    {
        foreach (var state in _activeMoves.Values)
        {
            state.Sprite.Position = state.To;
        }

        _activeMoves.Clear();
    }

    private static float EaseOutCubic(float progress)
    {
        var inverse = 1f - progress;
        return 1f - (inverse * inverse * inverse);
    }

    private static Vector2 Lerp(Vector2 from, Vector2 to, float weight)
    {
        return from + ((to - from) * weight);
    }
}