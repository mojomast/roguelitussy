using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed class EntityRenderer
{
    private const float EntitySpriteMaxWidth = WorldView.TileSize - 8f;
    private const float EntitySpriteMaxHeight = WorldView.TileSize - 12f;
    private const float FallbackBodyTopInset = 2f;
    private const float MaxAnimatedDriftPixels = WorldView.TileSize * 1.25f;
    private readonly Dictionary<EntityId, Node2D> _sprites = new();
    private readonly Dictionary<EntityId, Position> _lastKnownPositions = new();
    private readonly AnimationController _animationController;
    private readonly HashSet<Position> _visibleTiles = new();
    private Node2D _layer;
    private IWorldState? _world;
    private IContentDatabase? _content;

    public EntityRenderer(Node2D? layer = null, AnimationController? animationController = null, IContentDatabase? content = null)
    {
        _layer = layer ?? new Node2D { Name = "EntityLayer" };
        _animationController = animationController ?? new AnimationController();
        _content = content;
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
        if (world is WorldState mutableWorld)
        {
            _content = mutableWorld.ContentDatabase;
        }

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
        SyncStatusOverlays(entity, root);
        if (!isExisting || !_animationController.IsMoveAnimating(entity.Id))
        {
            root.Position = WorldView.ToCanvasPosition(entity.Position);
        }
        root.Visible = IsEntityVisible(entity.Id, entity.Position);
        if (!_animationController.IsDamageFlashing(entity.Id))
        {
            root.Modulate = Colors.White;
        }
        _lastKnownPositions[entity.Id] = entity.Position;
    }

    public void RefreshStatusOverlays(EntityId entityId)
    {
        var entity = _world?.GetEntity(entityId);
        if (entity is null || !_sprites.TryGetValue(entityId, out var spriteRoot))
        {
            return;
        }

        SyncStatusOverlays(entity, spriteRoot);
    }

    private void SyncStatusOverlays(IEntity entity, Node2D spriteRoot)
    {
        var container = GetOrCreateChild<Node2D>(spriteRoot, "StatusOverlay");
        var effectsComponent = entity.GetComponent<StatusEffectsComponent>();
        var effects = effectsComponent?.Effects ?? Array.Empty<StatusEffectInstance>();
        var desired = new Dictionary<string, StatusEffectInstance>();
        foreach (var effect in effects)
        {
            var statusId = StatusEffectIdFromType(effect.Type);
            if (!string.IsNullOrWhiteSpace(statusId))
            {
                desired[statusId] = effect;
            }
        }

        foreach (var child in container.GetChildren().ToArray())
        {
            if (child is not Sprite2D sprite || string.IsNullOrWhiteSpace(sprite.Name))
            {
                continue;
            }

            if (!desired.ContainsKey(sprite.Name.ToString()))
            {
                container.RemoveChild(sprite);
                sprite.QueueFree();
            }
        }

        var index = 0;
        foreach (var (statusId, effect) in desired)
        {
            if (_content?.TryGetStatusEffect(statusId, out var definition) != true || definition is null)
            {
                continue;
            }

            var icon = GetOrCreateChild<Sprite2D>(container, statusId);
            icon.Texture = LoadStatusIcon(definition.IconPath);
            icon.Modulate = ParseTint(definition.ColorTint);
            icon.Position = new Vector2(index * 10f, -6f);
            icon.Scale = new Vector2(0.5f, 0.5f);
            icon.ZIndex = 10;
            index++;
        }
    }

    private static Texture2D? LoadStatusIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        try
        {
            return GD.Load<Texture2D>(iconPath);
        }
        catch
        {
            return null;
        }
    }

    private static Color ParseTint(string colorTint)
    {
        if (string.IsNullOrWhiteSpace(colorTint))
        {
            return Colors.White;
        }

        var parsed = ParseHtmlColor(colorTint);
        return parsed ?? Colors.White;
    }

    private static Color? ParseHtmlColor(string value)
    {
        var span = value.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length == 6 && TryParseHexByte(span[..2], out var r6) && TryParseHexByte(span[2..4], out var g6) && TryParseHexByte(span[4..6], out var b6))
        {
            return new Color(r6 / 255f, g6 / 255f, b6 / 255f, 1f);
        }

        if (span.Length == 8 && TryParseHexByte(span[..2], out var r8) && TryParseHexByte(span[2..4], out var g8) && TryParseHexByte(span[4..6], out var b8) && TryParseHexByte(span[6..8], out var a8))
        {
            return new Color(r8 / 255f, g8 / 255f, b8 / 255f, a8 / 255f);
        }

        return null;
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> chars, out byte value)
    {
        value = 0;
        if (chars.Length != 2)
        {
            return false;
        }

        return byte.TryParse(chars, System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    private static string? StatusEffectIdFromType(StatusEffectType type) => type switch
    {
        StatusEffectType.Poisoned => "poisoned",
        StatusEffectType.Burning => "burning",
        StatusEffectType.Frozen => "frozen",
        StatusEffectType.Stunned => "stunned",
        StatusEffectType.Hasted => "haste",
        StatusEffectType.Invisible => null,
        StatusEffectType.Regenerating => "regenerating",
        StatusEffectType.Weakened => "weakened",
        StatusEffectType.Shielded => "shielded",
        StatusEffectType.Empowered => "empowered",
        StatusEffectType.Corroded => "corroded",
        StatusEffectType.Phased => "phased",
        StatusEffectType.Flying => "flying",
        _ => null,
    };

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
                spriteRoot.Visible = IsEntityVisible(entityId, position);
            }
        }
    }

    public bool HasSprite(EntityId entityId) => _sprites.ContainsKey(entityId);

    public Node2D? GetSprite(EntityId entityId) => _sprites.TryGetValue(entityId, out var spriteRoot) ? spriteRoot : null;

    public Position? GetLastKnownPosition(EntityId entityId) =>
        _lastKnownPositions.TryGetValue(entityId, out var position) ? position : null;

    private bool IsEntityVisible(EntityId entityId, Position position)
    {
        if (_world is null)
        {
            return _visibleTiles.Count == 0 || _visibleTiles.Contains(position);
        }

        if (_world.Player is not null && entityId == _world.Player.Id)
        {
            return true;
        }

        return _world.InBounds(position) && _world.IsVisible(position);
    }

    private static Node2D CreateSpriteRoot(IEntity entity)
    {
        var spriteRoot = new Node2D
        {
            Name = entity.Id.ToString(),
            Position = WorldView.ToCanvasPosition(entity.Position),
            Modulate = Colors.White,
        };

        EnsureBodyVisual(entity, spriteRoot);
        ApplyAppearance(entity, spriteRoot);
        return spriteRoot;
    }

    private static void ApplyAppearance(IEntity entity, Node2D spriteRoot)
    {
        EnsureBodyVisual(entity, spriteRoot);

        if (FindChild<Sprite2D>(spriteRoot, "Body") is { } spriteBody)
        {
            spriteBody.Modulate = entity.Faction is Faction.Player or Faction.Neutral
                ? PlayerVisualCatalog.Resolve(entity).BodyTint
                : ResolveTextureTint(entity);
        }

        if (FindChild<ColorRect>(spriteRoot, "Body") is { } rectBody)
        {
            rectBody.Color = ResolveFallbackTint(entity);
        }

        if (entity.GetComponent<ChestComponent>() is not null)
        {
            RemoveChild(spriteRoot, "AccentBand");
            RemoveChild(spriteRoot, "VariantSigil");
            RemoveChild(spriteRoot, "VariantDetail");

            var chestBand = GetOrCreateChild<ColorRect>(spriteRoot, "ChestBand");
            chestBand.Position = new Vector2(4f, 10f);
            chestBand.Size = new Vector2(WorldView.TileSize - 8f, 6f);
            chestBand.Color = new Color(0.35f, 0.2f, 0.08f, 1f);

            var chestLatch = GetOrCreateChild<Label>(spriteRoot, "ChestLatch");
            chestLatch.Position = new Vector2(11f, 9f);
            chestLatch.Text = "C";
            chestLatch.Modulate = new Color(0.98f, 0.87f, 0.48f, 1f);
            return;
        }

        RemoveChild(spriteRoot, "ChestBand");
        RemoveChild(spriteRoot, "ChestLatch");

        if (entity.Faction != Faction.Player)
        {
            RemoveChild(spriteRoot, "AccentBand");
            RemoveChild(spriteRoot, "VariantSigil");
            RemoveChild(spriteRoot, "VariantDetail");
            return;
        }

        RemoveChild(spriteRoot, "AccentBand");
        RemoveChild(spriteRoot, "VariantSigil");
        RemoveChild(spriteRoot, "VariantDetail");
    }

    private static void EnsureBodyVisual(IEntity entity, Node2D spriteRoot)
    {
        var texture = WorldArtCatalog.GetEntityTexture(entity);
        if (texture is not null)
        {
            RemoveChild(spriteRoot, "Body", typeof(ColorRect));

            var spriteBody = FindChild<Sprite2D>(spriteRoot, "Body") ?? GetOrCreateChild<Sprite2D>(spriteRoot, "Body");
            spriteBody.Texture = texture;
            var shouldCrop = ShouldCropPortraitHeadroom(texture);
            spriteBody.RegionEnabled = shouldCrop;
            spriteBody.RegionRect = shouldCrop
                ? new Rect2(new Vector2(0f, 8f), new Vector2(16f, 20f))
                : new Rect2();
            var displaySize = shouldCrop ? spriteBody.RegionRect.Size : ResolveSourceSize(texture);
            var displayScale = ResolveTextureScale(displaySize);
            spriteBody.Position = new Vector2(0f, ResolveBodyVerticalOffset(displaySize, displayScale));
            spriteBody.Scale = displayScale;
            return;
        }

        RemoveChild(spriteRoot, "Body", typeof(Sprite2D));

        var rectBody = FindChild<ColorRect>(spriteRoot, "Body") ?? GetOrCreateChild<ColorRect>(spriteRoot, "Body");
        rectBody.Position = new Vector2(2f, FallbackBodyTopInset);
        rectBody.Size = new Vector2(WorldView.TileSize - 4f, WorldView.TileSize - 4f);
        rectBody.Color = ResolveFallbackTint(entity);
    }

    private static bool ShouldCropPortraitHeadroom(Texture2D texture)
    {
        return texture.ResourcePath.EndsWith("Knight_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Knight_Female_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Elf_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Elf_Female_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Wizzard_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Wizzard_Female_Idle_1.png", StringComparison.Ordinal);
    }

    private static Vector2 ResolveTextureScale(Vector2 sourceSize)
    {
        var uniformScale = MathF.Min(EntitySpriteMaxWidth / sourceSize.X, EntitySpriteMaxHeight / sourceSize.Y);
        return new Vector2(uniformScale, uniformScale);
    }

    private static float ResolveBodyVerticalOffset(Vector2 sourceSize, Vector2 scale)
    {
        var scaledHeight = sourceSize.Y * scale.Y;
        return (WorldView.TileSize * 0.5f) - (scaledHeight * 0.5f);
    }

    private static Vector2 ResolveSourceSize(Texture2D texture)
    {
        if (texture is AtlasTexture atlas)
        {
            return atlas.Region.Size;
        }

        if (texture.ResourcePath.EndsWith("Big_Demon_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(32f, 36f);
        }

        if (texture.ResourcePath.EndsWith("Big_Zombie_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(32f, 34f);
        }

        if (texture.ResourcePath.EndsWith("Ogre_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(32f, 32f);
        }

        if (texture.ResourcePath.EndsWith("Elf_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Elf_Female_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Knight_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Knight_Female_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Wizzard_Male_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Wizzard_Female_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(16f, 28f);
        }

        if (texture.ResourcePath.EndsWith("Chort_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(16f, 24f);
        }

        if (texture.ResourcePath.EndsWith("Masked_Orc_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Necromancer_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Orc_Shaman_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Orc_Warrior_Idle_1.png", StringComparison.Ordinal)
            || texture.ResourcePath.EndsWith("Wogol_Idle_1.png", StringComparison.Ordinal))
        {
            return new Vector2(16f, 20f);
        }

        return new Vector2(16f, 16f);
    }

    private static Color ResolveFallbackTint(IEntity entity)
    {
        if (entity.GetComponent<ChestComponent>() is not null)
        {
            return new Color(0.67f, 0.46f, 0.19f, 1f);
        }

        if (entity.Faction is Faction.Player or Faction.Neutral)
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
        RemoveChild(parent, name, null);
    }

    private static void RemoveChild(Node parent, string name, Type? type)
    {
        foreach (var child in parent.GetChildren().ToArray())
        {
            if (child.Name != name || (type is not null && child.GetType() != type))
            {
                continue;
            }

            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
