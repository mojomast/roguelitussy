using Godot;

namespace Roguelike.Godot;

public partial class Tooltip : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _nameLabel = null!;
    private Label _descriptionLabel = null!;
    private Label _statsLabel = null!;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%TooltipPanel");
        _nameLabel = GetNode<Label>("%TooltipName");
        _descriptionLabel = GetNode<Label>("%TooltipDescription");
        _statsLabel = GetNode<Label>("%TooltipStats");

        _panel.Visible = false;
    }

    public void Show(string name, string description, string stats, Vector2 position)
    {
        _nameLabel.Text = name;
        _descriptionLabel.Text = description;
        _statsLabel.Text = stats;
        _panel.Position = position;
        _panel.Visible = true;
    }

    public new void Hide()
    {
        _panel.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_panel.Visible)
        {
            var viewport = GetViewport();
            var mousePos = viewport.GetMousePosition();
            var viewportSize = viewport.GetVisibleRect().Size;

            // Keep tooltip on screen
            var panelSize = _panel.Size;
            var x = Mathf.Min(mousePos.X + 16, viewportSize.X - panelSize.X);
            var y = Mathf.Min(mousePos.Y + 16, viewportSize.Y - panelSize.Y);
            _panel.Position = new Vector2(x, y);
        }
    }
}
