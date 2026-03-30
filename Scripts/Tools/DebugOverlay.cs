using Godot;

namespace Roguelike.Godot;

public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;
    private bool _visible;

    public override void _Ready()
    {
        Layer = 99;
        _label = GetNode<Label>("Label");
        _label.Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_visible) return;

        int fps = (int)Engine.GetFramesPerSecond();
        int entityCount = 0;
        int depth = 0;
        int turn = 0;
        string playerPos = "N/A";

        if (GameManager.Instance is { } gm)
        {
            var world = gm.GetMeta("WorldState", default(Variant));
            // WorldState access will be wired once GameManager exposes it.
            // For now we display FPS and placeholder values.
        }

        _label.Text = $"FPS: {fps}\nEntities: {entityCount}\nDepth: {depth}  Turn: {turn}\nPos: {playerPos}";
    }

    public void Toggle()
    {
        _visible = !_visible;
        _label.Visible = _visible;
    }

    public void SetStats(int entityCount, int depth, int turn, string playerPos)
    {
        // Called externally when WorldState is available.
        // Will be wired up when GameManager exposes world state.
    }
}
