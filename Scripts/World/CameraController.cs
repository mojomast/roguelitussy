using Godot;
using Roguelike.Core;

namespace Roguelike.Godot;

public partial class CameraController : Camera2D
{
    private const int TileSize = 16;
    private const float LerpSpeed = 8f;

    private Position _targetTile;
    private Vector2 _targetPixel;
    private int _mapWidth;
    private int _mapHeight;

    public override void _Ready()
    {
        Enabled = true;
    }

    public void SetMapBounds(int width, int height)
    {
        _mapWidth = width;
        _mapHeight = height;

        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = width * TileSize;
        LimitBottom = height * TileSize;
    }

    public void SetTarget(Position pos)
    {
        _targetTile = pos;
        _targetPixel = new Vector2(
            pos.X * TileSize + TileSize / 2f,
            pos.Y * TileSize + TileSize / 2f);
    }

    public void SnapToTarget()
    {
        Position = _targetPixel;
    }

    public override void _Process(double delta)
    {
        Position = Position.Lerp(_targetPixel, (float)(LerpSpeed * delta));
    }
}
