using Godot;

namespace Godotussy;

public partial class RoguelikeToolsPlugin : EditorPlugin
{
    public MapEditor? MapEditorDock { get; private set; }

    public ItemEditor? ItemEditorDock { get; private set; }

    public override void _EnterTree()
    {
        MapEditorDock = new MapEditor();
        ItemEditorDock = new ItemEditor();
        AddControlToBottomPanel(MapEditorDock, "Map Editor");
        AddControlToBottomPanel(ItemEditorDock, "Content Editor");
    }

    public override void _ExitTree()
    {
        if (MapEditorDock is not null)
        {
            RemoveControlFromBottomPanel(MapEditorDock);
            MapEditorDock.QueueFree();
            MapEditorDock = null;
        }

        if (ItemEditorDock is not null)
        {
            RemoveControlFromBottomPanel(ItemEditorDock);
            ItemEditorDock.QueueFree();
            ItemEditorDock = null;
        }
    }
}