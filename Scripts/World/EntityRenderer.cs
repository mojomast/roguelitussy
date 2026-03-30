using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Roguelike.Godot;

public partial class EntityRenderer : Node2D
{
    private const int TileSize = 16;

    private readonly Dictionary<string, Sprite2D> _sprites = new();
    private IWorldState _world = null!;
    private AnimationController _anim = null!;

    public void Initialize(IWorldState world, AnimationController anim)
    {
        _world = world;
        _anim = anim;
    }

    public void BuildSprites()
    {
        ClearSprites();

        foreach (var entity in _world.Entities)
        {
            CreateSprite(entity);
        }
    }

    private Sprite2D CreateSprite(IEntity entity)
    {
        var sprite = new Sprite2D();
        sprite.Name = $"Entity_{entity.Id}";
        sprite.Position = new Vector2(
            entity.Position.X * TileSize,
            entity.Position.Y * TileSize);
        sprite.Centered = false;
        sprite.ZIndex = entity.Faction == Faction.Player ? 10 : 5;

        // Placeholder colored rect texture
        var img = Image.CreateEmpty(TileSize, TileSize, false, Image.Format.Rgba8);
        var color = GetEntityColor(entity);
        img.Fill(color);
        sprite.Texture = ImageTexture.CreateFromImage(img);

        AddChild(sprite);
        _sprites[entity.Id.ToString()] = sprite;
        return sprite;
    }

    private static Color GetEntityColor(IEntity entity)
    {
        return entity.Faction switch
        {
            Faction.Player => new Color(0.2f, 0.6f, 1f),
            Faction.Enemy => new Color(0.9f, 0.2f, 0.2f),
            Faction.Neutral => new Color(0.8f, 0.8f, 0.3f),
            _ => new Color(0.7f, 0.7f, 0.7f),
        };
    }

    public void OnEntityMoved(string entityId, int fromX, int fromY, int toX, int toY)
    {
        if (!_sprites.TryGetValue(entityId, out var sprite))
            return;

        _anim.AnimateMove(sprite, new Position(fromX, fromY), new Position(toX, toY));
    }

    public void OnEntityAttacked(string attackerId, string defenderId, int damage, bool isCrit, bool isMiss)
    {
        if (_sprites.TryGetValue(attackerId, out var attackerSprite))
        {
            var attacker = FindEntity(attackerId);
            var defender = FindEntity(defenderId);
            if (attacker != null && defender != null)
            {
                _anim.AnimateAttack(attackerSprite, attacker.Position, defender.Position);
            }
        }

        if (!isMiss && _sprites.TryGetValue(defenderId, out _))
        {
            var defender = FindEntity(defenderId);
            if (defender != null)
            {
                _anim.SpawnDamagePopup(this, defender.Position, damage, isCrit);
            }
        }
    }

    public void OnEntityDied(string entityId, string _killerId)
    {
        if (_sprites.TryGetValue(entityId, out var sprite))
        {
            _sprites.Remove(entityId);
            _anim.AnimateDeath(sprite);
        }
    }

    public void UpdateVisibility()
    {
        foreach (var (idStr, sprite) in _sprites)
        {
            var entity = FindEntity(idStr);
            if (entity == null)
            {
                sprite.Visible = false;
                continue;
            }
            sprite.Visible = _world.IsVisible(entity.Position);
        }
    }

    private IEntity? FindEntity(string idStr)
    {
        var id = EntityId.From(idStr);
        return _world.GetEntity(id);
    }

    private void ClearSprites()
    {
        foreach (var sprite in _sprites.Values)
        {
            sprite.QueueFree();
        }
        _sprites.Clear();
    }
}
