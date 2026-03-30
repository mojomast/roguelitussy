using Godot;
using Roguelike.Core;

namespace Roguelike.Godot;

public partial class AnimationController : Node
{
    private const float MoveDuration = 0.15f;
    private const float AttackBumpDuration = 0.1f;
    private const float DeathDuration = 0.3f;
    private const int TileSize = 16;

    public Tween AnimateMove(Node2D sprite, Position from, Position to)
    {
        var startPos = new Vector2(from.X * TileSize, from.Y * TileSize);
        var endPos = new Vector2(to.X * TileSize, to.Y * TileSize);

        sprite.Position = startPos;
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "position", endPos, MoveDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        return tween;
    }

    public Tween AnimateAttack(Node2D sprite, Position attackerPos, Position targetPos)
    {
        var basePos = new Vector2(attackerPos.X * TileSize, attackerPos.Y * TileSize);
        var dir = new Vector2(targetPos.X - attackerPos.X, targetPos.Y - attackerPos.Y).Normalized();
        var bumpPos = basePos + dir * (TileSize * 0.4f);

        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "position", bumpPos, AttackBumpDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position", basePos, AttackBumpDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        return tween;
    }

    public Tween AnimateDeath(Node2D sprite)
    {
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate:a", 0f, DeathDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(sprite.QueueFree));
        return tween;
    }

    public void SpawnDamagePopup(Node parent, Position pos, int damage, bool isCrit, bool isHeal = false)
    {
        var popup = new DamagePopup();
        parent.AddChild(popup);
        popup.Position = new Vector2(pos.X * TileSize + TileSize / 2f, pos.Y * TileSize);
        popup.Setup(damage, isCrit, isHeal);
    }
}
