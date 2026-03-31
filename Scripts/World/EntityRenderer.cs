using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed class EntityRenderer
{
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
        if (!_sprites.TryGetValue(entity.Id, out var spriteRoot))
        {
            spriteRoot = CreateSpriteRoot(entity);
            _sprites[entity.Id] = spriteRoot;
            _layer.AddChild(spriteRoot);
        }

        spriteRoot.Position = WorldView.ToCanvasPosition(entity.Position);
        spriteRoot.Visible = _visibleTiles.Count == 0 || _visibleTiles.Contains(entity.Position);
        spriteRoot.Modulate = Colors.White;
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
                Texture = texture,
            });
            return spriteRoot;
        }

        spriteRoot.AddChild(new ColorRect
        {
            Name = "Body",
            Position = new Vector2(2f, 2f),
            Size = new Vector2(WorldView.TileSize - 4f, WorldView.TileSize - 4f),
            Color = ResolveTint(entity),
        });

        return spriteRoot;
    }

    private static Color ResolveTint(IEntity entity)
    {
        return entity.Faction switch
        {
            Faction.Player => new Color(0.25f, 0.85f, 0.35f, 1f),
            Faction.Enemy => new Color(0.85f, 0.25f, 0.25f, 1f),
            _ => new Color(0.95f, 0.85f, 0.35f, 1f),
        };
    }
}