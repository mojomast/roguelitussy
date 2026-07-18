using Godot;

namespace Godotussy;

/// <summary>
/// Shared rendering colors for world-view visuals and feedback effects.
/// Centralizes the tints previously scattered across WorldView and EntityRenderer.
/// </summary>
public static class RenderPalette
{
    // Popups
    /// <summary>Gold tint used for item pickup text popups.</summary>
    public static readonly Color PickupPopup = new(0.98f, 0.87f, 0.42f, 1f);

    // Walkable-boundary trim and wall covers
    public static readonly Color BoundaryTrim = new(0.79f, 0.71f, 0.63f, 0.95f);
    public static readonly Color BoundaryShadow = new(0.11f, 0.08f, 0.07f, 0.35f);
    public static readonly Color WallCoverFace = new(0.17f, 0.13f, 0.12f, 0.96f);
    public static readonly Color WallCoverShadow = new(0.08f, 0.06f, 0.05f, 0.7f);
    public static readonly Color WallCoverSideShadow = new(0.08f, 0.06f, 0.05f, 0.42f);
    public static readonly Color WallStripShadow = new(0.08f, 0.06f, 0.05f, 0.55f);

    // Fallback tile colors (used when textured tile art is unavailable)
    public static readonly Color TileFloor = new(0.18f, 0.18f, 0.2f, 1f);
    public static readonly Color TileWall = new(0.07f, 0.07f, 0.09f, 1f);
    public static readonly Color TileDoor = new(0.58f, 0.36f, 0.12f, 1f);
    public static readonly Color TileStairsDown = new(0.2f, 0.42f, 0.75f, 1f);
    public static readonly Color TileStairsUp = new(0.32f, 0.58f, 0.28f, 1f);
    public static readonly Color TileWater = new(0.12f, 0.24f, 0.55f, 1f);
    public static readonly Color TileLava = new(0.7f, 0.24f, 0.08f, 1f);
    public static readonly Color TileUnknown = new(0f, 0f, 0f, 1f);

    // Targeting overlay
    public static readonly Color TargetingPreviewValid = new(0.9f, 0.4f, 0.1f, 0.35f);
    public static readonly Color TargetingPreviewInvalid = new(0.9f, 0.1f, 0.1f, 0.35f);
    public static readonly Color TargetingCursorValid = new(0.95f, 0.85f, 0.2f, 0.85f);
    public static readonly Color TargetingCursorInvalid = new(0.95f, 0.15f, 0.15f, 0.85f);

    // Chest accents
    public static readonly Color ChestBody = new(0.67f, 0.46f, 0.19f, 1f);
    public static readonly Color ChestBand = new(0.35f, 0.2f, 0.08f, 1f);
    public static readonly Color ChestLatch = new(0.98f, 0.87f, 0.48f, 1f);

    // Fallback entity tints (untextured bodies)
    public static readonly Color FallbackRat = new(0.62f, 0.55f, 0.48f, 1f);
    public static readonly Color FallbackSpider = new(0.36f, 0.42f, 0.52f, 1f);
    public static readonly Color FallbackSlime = new(0.34f, 0.72f, 0.42f, 1f);
    public static readonly Color FallbackWraith = new(0.62f, 0.54f, 0.82f, 1f);
    public static readonly Color FallbackDemon = new(0.94f, 0.44f, 0.18f, 1f);
    public static readonly Color FallbackEnemy = new(0.82f, 0.28f, 0.28f, 1f);
    public static readonly Color FallbackNeutral = new(0.95f, 0.85f, 0.35f, 1f);
}
