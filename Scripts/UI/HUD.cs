using Godot;

namespace Roguelike.Godot;

public partial class HUD : CanvasLayer
{
    private ProgressBar _hpBar = null!;
    private Label _hpLabel = null!;
    private Label _depthLabel = null!;
    private Label _turnLabel = null!;

    public override void _Ready()
    {
        _hpBar = GetNode<ProgressBar>("%HPBar");
        _hpLabel = GetNode<Label>("%HPLabel");
        _depthLabel = GetNode<Label>("%DepthLabel");
        _turnLabel = GetNode<Label>("%TurnLabel");

        var bus = EventBus.Instance;
        bus.EntityHealthChanged += OnEntityHealthChanged;
        bus.TurnStarted += OnTurnStarted;
        bus.LevelGenerated += OnLevelGenerated;

        UpdateHP(0, 0, 1);
        _depthLabel.Text = "Depth: 1";
        _turnLabel.Text = "Turn: 0";
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.EntityHealthChanged -= OnEntityHealthChanged;
        bus.TurnStarted -= OnTurnStarted;
        bus.LevelGenerated -= OnLevelGenerated;
    }

    private void OnEntityHealthChanged(string entityId, int oldHP, int newHP, int maxHP)
    {
        if (entityId != "player") return;
        UpdateHP(oldHP, newHP, maxHP);
    }

    private void UpdateHP(int _oldHP, int newHP, int maxHP)
    {
        _hpBar.MaxValue = maxHP;
        _hpBar.Value = newHP;
        _hpLabel.Text = $"HP: {newHP} / {maxHP}";
    }

    private void OnTurnStarted(int turnNumber)
    {
        _turnLabel.Text = $"Turn: {turnNumber}";
    }

    private void OnLevelGenerated(int depth, int width, int height)
    {
        _depthLabel.Text = $"Depth: {depth}";
    }
}
