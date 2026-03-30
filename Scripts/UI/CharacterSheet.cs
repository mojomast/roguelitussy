using Godot;

namespace Roguelike.Godot;

public partial class CharacterSheet : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _hpStat = null!;
    private Label _attackStat = null!;
    private Label _defenseStat = null!;
    private Label _accuracyStat = null!;
    private Label _evasionStat = null!;
    private Label _speedStat = null!;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%CharacterPanel");
        _hpStat = GetNode<Label>("%HPStat");
        _attackStat = GetNode<Label>("%AttackStat");
        _defenseStat = GetNode<Label>("%DefenseStat");
        _accuracyStat = GetNode<Label>("%AccuracyStat");
        _evasionStat = GetNode<Label>("%EvasionStat");
        _speedStat = GetNode<Label>("%SpeedStat");

        _panel.Visible = false;

        var bus = EventBus.Instance;
        bus.EntityHealthChanged += OnEntityHealthChanged;
        bus.EquipmentChanged += OnEquipmentChanged;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.EntityHealthChanged -= OnEntityHealthChanged;
        bus.EquipmentChanged -= OnEquipmentChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("character_sheet") ||
            (@event is InputEventKey key && key.Pressed && key.Keycode == Key.C))
        {
            _panel.Visible = !_panel.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnEntityHealthChanged(string entityId, int oldHP, int newHP, int maxHP)
    {
        if (entityId != "player") return;
        _hpStat.Text = $"HP: {newHP} / {maxHP}";
    }

    private void OnEquipmentChanged(string entityId, int slot, string itemTemplateId)
    {
        if (entityId != "player") return;
        // Stats would be recalculated from entity data
    }

    public void UpdateStats(int hp, int maxHp, int attack, int defense, int accuracy, int evasion, int speed)
    {
        _hpStat.Text = $"HP: {hp} / {maxHp}";
        _attackStat.Text = $"Attack: {attack}";
        _defenseStat.Text = $"Defense: {defense}";
        _accuracyStat.Text = $"Accuracy: {accuracy}";
        _evasionStat.Text = $"Evasion: {evasion}";
        _speedStat.Text = $"Speed: {speed}";
    }
}
