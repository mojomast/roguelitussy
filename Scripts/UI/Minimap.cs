using Godot;

namespace Roguelike.Godot;

public partial class Minimap : CanvasLayer
{
    private const int MapWidth = 200;
    private const int MapHeight = 150;

    private Control _mapDisplay = null!;
    private int _levelWidth;
    private int _levelHeight;

    public override void _Ready()
    {
        _mapDisplay = GetNode<Control>("%MapDisplay");
        _mapDisplay.CustomMinimumSize = new Vector2(MapWidth, MapHeight);

        var bus = EventBus.Instance;
        bus.LevelGenerated += OnLevelGenerated;
        bus.FOVUpdated += OnFOVUpdated;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.LevelGenerated -= OnLevelGenerated;
        bus.FOVUpdated -= OnFOVUpdated;
    }

    private void OnLevelGenerated(int depth, int width, int height)
    {
        _levelWidth = width;
        _levelHeight = height;
        _mapDisplay.QueueRedraw();
    }

    private void OnFOVUpdated()
    {
        _mapDisplay.QueueRedraw();
    }
}
