#if TOOLS
using Godot;

namespace Roguelike.Godot;

/// <summary>
/// Placeholder EditorScript for editing Content/items.json from the Godot editor.
/// Will be extended to provide a GUI for item template creation and editing.
/// </summary>
[Tool]
public partial class ItemEditor : EditorPlugin
{
    private const string ItemsPath = "res://Content/items.json";

    public override string _GetPluginName() => "Roguelike Item Editor";

    public override void _EnterTree()
    {
        GD.Print($"[ItemEditor] Scaffold loaded. Target: {ItemsPath}");
    }

    public override void _ExitTree()
    {
        GD.Print("[ItemEditor] Scaffold unloaded.");
    }
}
#endif
