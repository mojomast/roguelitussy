using Godot;

namespace Godotussy;

public partial class FloorEventPopupUI : Control
{
    private const double DefaultSeconds = 3d;
    private readonly Label _label = new() { Name = "FloorEventPopupLabel" };
    private double _remainingSeconds;

    public string MessageText { get; private set; } = string.Empty;

    public FloorEventPopupUI()
    {
        Name = "FloorEventPopupUI";
        Visible = false;
        AddChild(_label);
    }

    public void Bind(EventBus? eventBus)
    {
        if (eventBus is null)
        {
            return;
        }

        eventBus.CurseRoomEntered += () => ShowMessage("Dark power fills this room. Treasure glitters through the curse.");
    }

    public void ShowMessage(string message, double seconds = DefaultSeconds)
    {
        MessageText = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        _remainingSeconds = seconds;
        _label.Text = MessageText;
        _label.Modulate = UiStyle.BrightGold();
        Visible = !string.IsNullOrWhiteSpace(MessageText);
        Layout();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _remainingSeconds -= delta;
        if (_remainingSeconds <= 0d)
        {
            Visible = false;
        }
    }

    private void Layout()
    {
        var viewport = GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
        Size = new Vector2(620f, 48f);
        Position = new Vector2((viewport.X - Size.X) * 0.5f, viewport.Y * 0.24f);
        _label.Position = Vector2.Zero;
        _label.Size = Size;
    }
}
