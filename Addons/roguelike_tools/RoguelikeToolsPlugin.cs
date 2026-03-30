#if TOOLS
using Godot;

namespace Roguelike.Godot;

[Tool]
public partial class RoguelikeToolsPlugin : EditorPlugin
{
    private Control? _mapEditorButton;
    private Control? _itemEditorButton;

    public override void _EnterTree()
    {
        GD.Print("[RoguelikeTools] Plugin loaded.");

        _mapEditorButton = new Button { Text = "Map Editor" };
        ((Button)_mapEditorButton).Pressed += OnMapEditorPressed;
        AddControlToContainer(CustomControlContainer.Toolbar, _mapEditorButton);

        _itemEditorButton = new Button { Text = "Item Editor" };
        ((Button)_itemEditorButton).Pressed += OnItemEditorPressed;
        AddControlToContainer(CustomControlContainer.Toolbar, _itemEditorButton);
    }

    public override void _ExitTree()
    {
        if (_mapEditorButton is not null)
        {
            RemoveControlFromContainer(CustomControlContainer.Toolbar, _mapEditorButton);
            _mapEditorButton.QueueFree();
            _mapEditorButton = null;
        }

        if (_itemEditorButton is not null)
        {
            RemoveControlFromContainer(CustomControlContainer.Toolbar, _itemEditorButton);
            _itemEditorButton.QueueFree();
            _itemEditorButton = null;
        }

        GD.Print("[RoguelikeTools] Plugin unloaded.");
    }

    private void OnMapEditorPressed()
    {
        GD.Print("[RoguelikeTools] Map Editor clicked (not yet implemented).");
    }

    private void OnItemEditorPressed()
    {
        GD.Print("[RoguelikeTools] Item Editor clicked (not yet implemented).");
    }
}
#endif
