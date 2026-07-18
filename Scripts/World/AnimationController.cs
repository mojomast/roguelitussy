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

    private sealed class PopupAnimationState
    {
        public required DamagePopup Popup { get; init; }

        public required Vector2 Start { get; init; }

        public float ElapsedSeconds { get; set; }
    }

    private sealed class DamageFlashState
    {
        public required Node2D Sprite { get; init; }

        public float ElapsedSeconds { get; set; }

        public float DurationSeconds { get; init; }
    }

    private sealed class AttackAnimationState
    {
        public required Node2D Sprite { get; init; }

        public required Vector2 From { get; init; }

        public required Vector2 Lunge { get; init; }

        public float ElapsedSeconds { get; set; }

        public float DurationSeconds { get; init; }
    }

    private sealed class DeathAnimationState
    {
        public required Node2D Sprite { get; init; }

        public required Vector2 BaseScale { get; init; }

        public float ElapsedSeconds { get; set; }

        public float DurationSeconds { get; init; }

        public Action? OnComplete { get; init; }
    }

    private const float MoveDurationSeconds = 0.08f;
    private const float DamageFlashDurationSeconds = 0.12f;
    private const float AttackDurationSeconds = 0.16f;
    private const float DeathDurationSeconds = 0.28f;
    private const float DeathEndScaleFactor = 0.55f;
    private static readonly Color DamageFlashTint = new(1.6f, 1.35f, 1.2f, 1f);
    private readonly List<AnimationRecord> _history = new();
    private readonly Dictionary<EntityId, MoveAnimationState> _activeMoves = new();
    private readonly Dictionary<EntityId, DamageFlashState> _activeDamageFlashes = new();
    private readonly Dictionary<EntityId, AttackAnimationState> _activeAttacks = new();
    private readonly Dictionary<EntityId, DeathAnimationState> _activeDeaths = new();
    private readonly List<PopupAnimationState> _activePopups = new();

    public IReadOnlyList<AnimationRecord> History => _history;

    public int ActiveMoveCount => _activeMoves.Count;

    public int ActivePopupCount => _activePopups.Count;

    public int ActiveFlashCount => _activeDamageFlashes.Count;

    public int ActiveAttackCount => _activeAttacks.Count;

    public int ActiveDeathCount => _activeDeaths.Count;

    public bool IsMoveAnimating(EntityId entityId) => _activeMoves.ContainsKey(entityId);

    public bool IsDamageFlashing(EntityId entityId) => _activeDamageFlashes.ContainsKey(entityId);

    public bool IsAttackAnimating(EntityId entityId) => _activeAttacks.ContainsKey(entityId);

    public bool IsDeathAnimating(EntityId entityId) => _activeDeaths.ContainsKey(entityId);

    public Vector2? GetMoveTarget(EntityId entityId) =>
        _activeMoves.TryGetValue(entityId, out var state) ? state.To : null;

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
        // Preserve the logical resting position if a previous lunge is still in flight.
        var startPosition = _activeAttacks.TryGetValue(entityId, out var previous)
            ? previous.From
            : sprite.Position;
        var lunge = startPosition + ((targetPosition - startPosition) * 0.3f);
        _activeAttacks[entityId] = new AttackAnimationState
        {
            Sprite = sprite,
            From = startPosition,
            Lunge = lunge,
            ElapsedSeconds = 0f,
            DurationSeconds = AttackDurationSeconds,
        };
        _history.Add(new AnimationRecord(AnimationType.Attack, entityId, startPosition, lunge));
    }

    public void AnimateDamage(EntityId entityId, Node2D sprite)
    {
        sprite.Modulate = DamageFlashTint;
        _activeDamageFlashes[entityId] = new DamageFlashState
        {
            Sprite = sprite,
            ElapsedSeconds = 0f,
            DurationSeconds = DamageFlashDurationSeconds,
        };
        _history.Add(new AnimationRecord(AnimationType.Damage, entityId, sprite.Position, sprite.Position));
    }

    public void AnimateDeath(EntityId entityId, Node2D sprite, Action? onComplete = null)
    {
        // A dying sprite should not keep moving or flashing.
        _activeMoves.Remove(entityId);
        _activeDamageFlashes.Remove(entityId);
        _activeAttacks.Remove(entityId);

        _activeDeaths[entityId] = new DeathAnimationState
        {
            Sprite = sprite,
            BaseScale = sprite.Scale,
            ElapsedSeconds = 0f,
            DurationSeconds = DeathDurationSeconds,
            OnComplete = onComplete,
        };
        _history.Add(new AnimationRecord(AnimationType.Death, entityId, sprite.Position, sprite.Position));
    }

    public void SpawnDamagePopup(Node parent, Vector2 position, int amount, bool isCrit, bool isHeal, bool isMiss = false)
    {
        var popup = new DamagePopup();
        popup.Position = position;
        popup.Setup(amount, isCrit, isHeal, isMiss);
        parent.AddChild(popup);
        _activePopups.Add(new PopupAnimationState
        {
            Popup = popup,
            Start = position,
            ElapsedSeconds = 0f,
        });
    }

    public void SpawnTextPopup(Node parent, Vector2 position, string text, Color color)
    {
        var popup = new DamagePopup();
        popup.Position = position;
        popup.SetupText(text, color);
        parent.AddChild(popup);
        _activePopups.Add(new PopupAnimationState
        {
            Popup = popup,
            Start = position,
            ElapsedSeconds = 0f,
        });
    }

    public void ClearHistory()
    {
        _history.Clear();
    }

    public void Advance(double delta)
    {
        if (_activeMoves.Count == 0
            && _activeDamageFlashes.Count == 0
            && _activePopups.Count == 0
            && _activeAttacks.Count == 0
            && _activeDeaths.Count == 0)
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

        foreach (var entry in _activeDamageFlashes.ToArray())
        {
            var state = entry.Value;
            state.ElapsedSeconds += (float)delta;
            var progress = Math.Clamp(state.ElapsedSeconds / state.DurationSeconds, 0f, 1f);
            state.Sprite.Modulate = Lerp(DamageFlashTint, Colors.White, progress);

            if (progress >= 1f)
            {
                state.Sprite.Modulate = Colors.White;
                _activeDamageFlashes.Remove(entry.Key);
            }
        }

        foreach (var entry in _activeAttacks.ToArray())
        {
            var state = entry.Value;
            state.ElapsedSeconds += (float)delta;
            var progress = Math.Clamp(state.ElapsedSeconds / state.DurationSeconds, 0f, 1f);

            // There-and-back lunge: out towards the target for the first half,
            // then ease back to the resting position.
            if (progress < 0.5f)
            {
                var outEased = EaseOutCubic(progress * 2f);
                state.Sprite.Position = Lerp(state.From, state.Lunge, outEased);
            }
            else
            {
                var backEased = EaseOutCubic((progress - 0.5f) * 2f);
                state.Sprite.Position = Lerp(state.Lunge, state.From, backEased);
            }

            if (progress >= 1f)
            {
                state.Sprite.Position = state.From;
                _activeAttacks.Remove(entry.Key);
            }
        }

        foreach (var entry in _activeDeaths.ToArray())
        {
            var state = entry.Value;
            state.ElapsedSeconds += (float)delta;
            var progress = Math.Clamp(state.ElapsedSeconds / state.DurationSeconds, 0f, 1f);

            // Fade out while collapsing slightly for a short "pop" before the node is freed.
            var eased = EaseOutCubic(progress);
            var scaleFactor = 1f - ((1f - DeathEndScaleFactor) * eased);
            state.Sprite.Modulate = new Color(1f, 1f, 1f, 1f - eased);
            state.Sprite.Scale = state.BaseScale * scaleFactor;

            if (progress >= 1f)
            {
                state.Sprite.Visible = false;
                _activeDeaths.Remove(entry.Key);
                state.OnComplete?.Invoke();
            }
        }

        foreach (var popupState in _activePopups.ToArray())
        {
            popupState.ElapsedSeconds += (float)delta;
            var progress = Math.Clamp(popupState.ElapsedSeconds / DamagePopup.Duration, 0f, 1f);
            var eased = EaseOutCubic(progress);
            popupState.Popup.Position = popupState.Start + new Vector2(0f, -DamagePopup.RiseDistance * eased);
            var baseColor = popupState.Popup.BaseColor;
            popupState.Popup.Modulate = new Color(baseColor.R, baseColor.G, baseColor.B, baseColor.A * (1f - progress));

            if (progress >= 1f)
            {
                popupState.Popup.QueueFree();
                _activePopups.Remove(popupState);
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

        foreach (var state in _activeDamageFlashes.Values)
        {
            state.Sprite.Modulate = Colors.White;
        }

        _activeDamageFlashes.Clear();

        foreach (var state in _activeAttacks.Values)
        {
            state.Sprite.Position = state.From;
        }

        _activeAttacks.Clear();

        foreach (var state in _activeDeaths.Values)
        {
            state.Sprite.Visible = false;
            state.Sprite.Modulate = Colors.Transparent;
            state.OnComplete?.Invoke();
        }

        _activeDeaths.Clear();

        foreach (var popupState in _activePopups)
        {
            popupState.Popup.QueueFree();
        }

        _activePopups.Clear();
    }

    public void CompleteMove(EntityId entityId)
    {
        if (!_activeMoves.TryGetValue(entityId, out var state))
        {
            return;
        }

        state.Sprite.Position = state.To;
        _activeMoves.Remove(entityId);
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

    private static Color Lerp(Color from, Color to, float weight)
    {
        return new Color(
            from.R + ((to.R - from.R) * weight),
            from.G + ((to.G - from.G) * weight),
            from.B + ((to.B - from.B) * weight),
            from.A + ((to.A - from.A) * weight));
    }
}
