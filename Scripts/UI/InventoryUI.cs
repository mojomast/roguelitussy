using Godot;
using System.Collections.Generic;

namespace Roguelike.Godot;

public partial class InventoryUI : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _itemList = null!;
    private Label _titleLabel = null!;
    private readonly List<string> _items = new();

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%InventoryPanel");
        _itemList = GetNode<VBoxContainer>("%ItemList");
        _titleLabel = GetNode<Label>("%InventoryTitle");

        _panel.Visible = false;

        var bus = EventBus.Instance;
        bus.ItemPickedUp += OnItemPickedUp;
        bus.ItemDropped += OnItemDropped;
        bus.ItemUsed += OnItemUsed;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.ItemPickedUp -= OnItemPickedUp;
        bus.ItemDropped -= OnItemDropped;
        bus.ItemUsed -= OnItemUsed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("inventory") ||
            (@event is InputEventKey key && key.Pressed && key.Keycode == Key.I))
        {
            ToggleInventory();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ToggleInventory()
    {
        _panel.Visible = !_panel.Visible;
    }

    private void OnItemPickedUp(string entityId, string itemTemplateId)
    {
        if (entityId != "player") return;
        _items.Add(itemTemplateId);
        RebuildList();
    }

    private void OnItemDropped(string entityId, string itemTemplateId, int posX, int posY)
    {
        if (entityId != "player") return;
        _items.Remove(itemTemplateId);
        RebuildList();
    }

    private void OnItemUsed(string entityId, string itemTemplateId, string effectDescription)
    {
        if (entityId != "player") return;
        _items.Remove(itemTemplateId);
        RebuildList();
    }

    private void RebuildList()
    {
        foreach (var child in _itemList.GetChildren())
        {
            child.QueueFree();
        }

        _titleLabel.Text = $"Inventory ({_items.Count})";

        foreach (var item in _items)
        {
            var label = new Label { Text = item };
            _itemList.AddChild(label);
        }
    }
}
