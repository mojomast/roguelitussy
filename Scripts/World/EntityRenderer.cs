using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed class EntityRenderer
{
    private static readonly Vector2 EntitySpriteScale = new(WorldView.TileSize / 16f, WorldView.TileSize / 16f);
    private const float MaxAnimatedDriftPixels = WorldView.TileSize * 1.25f;
    private readonly Dictionary<EntityId, Node2D> _sprites = new();
    private readonly Dictionary<EntityId, Position> _lastKnownPositions = new();
    private readonly AnimationController _animationController;
    private readonly HashSet<Position> _visibleTiles = new();
    private Node2D _layer;
    private IWorldState? _world;

    public EntityRenderer(Node2D? layer = null, AnimationController? animationController = null)
    {
        _layer = layer ?? new Node2D { Name = "EntityLayer" };
        _animationController = animationController ?? new AnimationController();
    }

    public Node2D Layer => _layer;

    public AnimationController Animations => _animationController;

    public int SpriteCount => _sprites.Count;

    public void BindLayer(Node2D layer)
    {
        _layer = layer;
    }

    public void BindWorld(IWorldState world)
    {
        _world = world;
        SyncEntities(world.Entities);
    }

    public void SyncEntities(IReadOnlyList<IEntity> entities)
    {
        var liveIds = new HashSet<EntityId>();
        foreach (var entity in entities)
        {
            liveIds.Add(entity.Id);
            UpsertEntity(entity);
        }

        foreach (var removedId in _sprites.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            RemoveEntity(removedId, animateDeath: false);
        }

        UpdateVisibility(_visibleTiles);
    }

    public void UpsertEntity(IEntity entity)
    {
        var isExisting = _sprites.TryGetValue(entity.Id, out var spriteRoot);
        if (!isExisting)
        {
            spriteRoot = CreateSpriteRoot(entity);
            _sprites[entity.Id] = spriteRoot;
            _layer.AddChild(spriteRoot);
        }

        var root = spriteRoot!;
        ApplyAppearance(entity, root);
        if (!isExisting || !_animationController.IsMoveAnimating(entity.Id))
        {
            root.Position = WorldView.ToCanvasPosition(entity.Position);
        }
        root.Visible = _visibleTiles.Count == 0 || _visibleTiles.Contains(entity.Position);
        root.Modulate = Colors.White;
        _lastKnownPositions[entity.Id] = entity.Position;
    }

    public void AnimateMove(EntityId entityId, Position targetPosition)
    {
        if (!_sprites.TryGetValue(entityId, out var spriteRoot))
        {
            return;
        }

        _animationController.AnimateMove(entityId, spriteRoot, WorldView.ToCanvasPosition(targetPosition));
        _lastKnownPositions[entityId] = targetPosition;
    }

    public void AnimateAttack(EntityId entityId, Position targetPosition)
    {
        if (!_sprites.TryGetValue(entityId, out var spriteRoot))
        {
            return;
        }

        _animationController.AnimateAttack(entityId, spriteRoot, WorldView.ToCanvasPosition(targetPosition));
    }

    public void AnimateDamage(EntityId entityId)
    {
        if (_sprites.TryGetValue(entityId, out var spriteRoot))
        {
            _animationController.AnimateDamage(entityId, spriteRoot);
        }
    }

    public void ReconcileEntityPositions(IReadOnlyList<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (!_sprites.TryGetValue(entity.Id, out var spriteRoot))
            {
                continue;
            }

            var targetPosition = WorldView.ToCanvasPosition(entity.Position);
            if (_animationController.GetMoveTarget(entity.Id) is { } moveTarget)
            {
                var hasUnexpectedTarget = !NearlyEqual(moveTarget, targetPosition);
                var driftPixels = GetAxisDrift(spriteRoot.Position, targetPosition);
                if (hasUnexpectedTarget || driftPixels > MaxAnimatedDriftPixels)
                {
                    _animationController.CompleteMove(entity.Id);
                }
            }

            if (!_animationController.IsMoveAnimating(entity.Id))
            {
                spriteRoot.Position = targetPosition;
            }

            _lastKnownPositions[entity.Id] = entity.Position;
        }
    }

    public void RemoveEntity(EntityId entityId, bool animateDeath)
    {
        if (!_sprites.TryGetValue(entityId, out var spriteRoot))
        {
            return;
        }

        if (animateDeath)
        {
            _animationController.AnimateDeath(entityId, spriteRoot);
        }

        _layer.RemoveChild(spriteRoot);
        spriteRoot.QueueFree();
        _sprites.Remove(entityId);
        _lastKnownPositions.Remove(entityId);
    }

    public void UpdateVisibility(IEnumerable<Position> visibleTiles)
    {
        _visibleTiles.Clear();
        foreach (var visibleTile in visibleTiles)
        {
            _visibleTiles.Add(visibleTile);
        }

        foreach (var (entityId, spriteRoot) in _sprites)
        {
            if (_lastKnownPositions.TryGetValue(entityId, out var position))
            {
                spriteRoot.Visible = _visibleTiles.Contains(position);
            }
        }
    }

    public bool HasSprite(EntityId entityId) => _sprites.ContainsKey(entityId);

    public Node2D? GetSprite(EntityId entityId) => _sprites.TryGetValue(entityId, out var spriteRoot) ? spriteRoot : null;

    public Position? GetLastKnownPosition(EntityId entityId) =>
        _lastKnownPositions.TryGetValue(entityId, out var position) ? position : null;

    private static Node2D CreateSpriteRoot(IEntity entity)
    {
        var spriteRoot = new Node2D
        {
            Name = entity.Id.ToString(),
            Position = WorldView.ToCanvasPosition(entity.Position),
            Modulate = Colors.White,
        };

        var texture = WorldArtCatalog.GetEntityTexture(entity);
        if (texture is not null)
        {
            spriteRoot.AddChild(new Sprite2D
            {
                Name = "Body",
                Position = new Vector2(0f, 0f),
                Scale = EntitySpriteScale,
                Texture = texture,
            });
            ApplyAppearance(entity, spriteRoot);
            return spriteRoot;
        }

        spriteRoot.AddChild(new ColorRect
        {
            Name = "Body",
            Position = new Vector2(2f, 2f),
            Size = new Vector2(WorldView.TileSize - 4f, WorldView.TileSize - 4f),
            Color = ResolveFallbackTint(entity),
        });

        ApplyAppearance(entity, spriteRoot);

        return spriteRoot;
    }

    private static void ApplyAppearance(IEntity entity, Node2D spriteRoot)
    {
        if (FindChild<Sprite2D>(spriteRoot, "Body") is { } spriteBody)
        {
            spriteBody.Texture = WorldArtCatalog.GetEntityTexture(entity);
            spriteBody.Modulate = entity.Faction == Faction.Player
                ? PlayerVisualCatalog.Resolve(entity).BodyTint
                : ResolveTextureTint(entity);
        }

        if (FindChild<ColorRect>(spriteRoot, "Body") is { } rectBody)
        {
            rectBody.Color = ResolveFallbackTint(entity);
        }

        if (entity.Faction != Faction.Player)
        {
            RemoveChild(spriteRoot, "AccentBand");
            RemoveChild(spriteRoot, "VariantSigil");
            RemoveChild(spriteRoot, "VariantDetail");
            return;
        }

        var profile = PlayerVisualCatalog.Resolve(entity);
        var accentBand = GetOrCreateChild<ColorRect>(spriteRoot, "AccentBand");
        accentBand.Position = new Vector2(6f, 28f);
        accentBand.Size = new Vector2(WorldView.TileSize - 12f, 4f);
        accentBand.Color = profile.AccentTint;

        var variantSigil = GetOrCreateChild<Label>(spriteRoot, "VariantSigil");
        variantSigil.Position = new Vector2(4f, -16f);
        variantSigil.Text = profile.RaceSigil;
        variantSigil.Modulate = profile.AccentTint;

        var variantDetail = GetOrCreateChild<Label>(spriteRoot, "VariantDetail");
        variantDetail.Position = new Vector2(18f, 8f);
        variantDetail.Text = profile.AppearanceMark;
        variantDetail.Modulate = profile.DetailTint;
    }

    private static Color ResolveFallbackTint(IEntity entity)
    {
        if (entity.Faction == Faction.Player)
        {
            return PlayerVisualCatalog.Resolve(entity).BodyTint;
        }

        var normalizedName = entity.Name.Trim().ToLowerInvariant();
        if (normalizedName.Contains("rat", System.StringComparison.Ordinal))
        {
            return new Color(0.62f, 0.55f, 0.48f, 1f);
        }

        if (normalizedName.Contains("spider", System.StringComparison.Ordinal))
        {
            return new Color(0.36f, 0.42f, 0.52f, 1f);
        }

        if (normalizedName.Contains("slime", System.StringComparison.Ordinal))
        {
            return new Color(0.34f, 0.72f, 0.42f, 1f);
        }

        if (normalizedName.Contains("wraith", System.StringComparison.Ordinal)
            || normalizedName.Contains("shadow", System.StringComparison.Ordinal))
        {
            return new Color(0.62f, 0.54f, 0.82f, 1f);
        }

        if (normalizedName.Contains("flame", System.StringComparison.Ordinal)
            || normalizedName.Contains("elemental", System.StringComparison.Ordinal)
            || normalizedName.Contains("demon", System.StringComparison.Ordinal))
        {
            return new Color(0.94f, 0.44f, 0.18f, 1f);
        }

        return entity.Faction switch
        {
            Faction.Enemy => new Color(0.82f, 0.28f, 0.28f, 1f),
            _ => new Color(0.95f, 0.85f, 0.35f, 1f),
        };
    }

    private static Color ResolveTextureTint(IEntity entity)
    {
        if (entity.Faction != Faction.Enemy)
        {
            return Colors.White;
        }

        var normalizedName = entity.Name.Trim().ToLowerInvariant();
        if (normalizedName.Contains("slime", StringComparison.Ordinal))
        {
            return new Color(0.78f, 1.0f, 0.78f, 1f);
        }

        if (normalizedName.Contains("wraith", StringComparison.Ordinal)
            || normalizedName.Contains("shadow", StringComparison.Ordinal))
        {
            return new Color(0.86f, 0.82f, 1.0f, 1f);
        }

        if (normalizedName.Contains("skeleton", StringComparison.Ordinal)
            || normalizedName.Contains("bone", StringComparison.Ordinal)
            || normalizedName.Contains("zombie", StringComparison.Ordinal))
        {
            return new Color(0.96f, 0.95f, 0.9f, 1f);
        }

        if (normalizedName.Contains("flame", StringComparison.Ordinal)
            || normalizedName.Contains("elemental", StringComparison.Ordinal)
            || normalizedName.Contains("demon", StringComparison.Ordinal))
        {
            return new Color(1.0f, 0.9f, 0.78f, 1f);
        }

        return Colors.White;
    }

    private static float GetAxisDrift(Vector2 current, Vector2 target)
    {
        var delta = current - target;
        return MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y));
    }

    private static bool NearlyEqual(Vector2 left, Vector2 right, float tolerance = 0.5f)
    {
        var delta = left - right;
        return MathF.Abs(delta.X) <= tolerance && MathF.Abs(delta.Y) <= tolerance;
    }

    private static T? FindChild<T>(Node parent, string name) where T : Node
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T typed && typed.Name == name)
            {
                return typed;
            }
        }

        return null;
    }

    private static T GetOrCreateChild<T>(Node parent, string name) where T : Node, new()
    {
        if (FindChild<T>(parent, name) is { } existing)
        {
            return existing;
        }

        var child = new T
        {
            Name = name,
        };
        parent.AddChild(child);
        return child;
    }

    private static void RemoveChild(Node parent, string name)
    {
        foreach (var child in parent.GetChildren().ToArray())
        {
            if (child.Name != name)
            {
                continue;
            }

            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}