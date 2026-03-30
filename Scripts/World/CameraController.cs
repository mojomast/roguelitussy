using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed class CameraController
{
    public const float DefaultZoom = 3f;
    private Camera2D? _camera;

    public Camera2D? Camera => _camera;

    public void Bind(Camera2D camera)
    {
        _camera = camera;
        _camera.Enabled = true;
        _camera.PositionSmoothingEnabled = true;
        _camera.PositionSmoothingSpeed = 10f;
        _camera.Zoom = new Vector2(DefaultZoom, DefaultZoom);
    }

    public void CenterOn(Position position, int tileSize)
    {
        if (_camera is null)
        {
            return;
        }

        _camera.Position = CenterOf(position, tileSize);
    }

    public static Vector2 CenterOf(Position position, int tileSize)
    {
        var halfTile = tileSize / 2f;
        return new Vector2((position.X * tileSize) + halfTile, (position.Y * tileSize) + halfTile);
    }
}