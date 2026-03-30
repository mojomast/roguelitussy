#if TOOLS
using Godot;

namespace Roguelike.Godot;

/// <summary>
/// Placeholder EditorPlugin for painting dungeon maps in the Godot editor.
/// Will be extended to support tile painting, room placement, and export.
/// </summary>
[Tool]
public partial class MapEditorPlugin : EditorPlugin
{
    public override string _GetPluginName() => "Roguelike Map Editor";

    public override void _EnterTree()
    {
        GD.Print("[MapEditor] Scaffold loaded.");
    }

    public override void _ExitTree()
    {
        GD.Print("[MapEditor] Scaffold unloaded.");
    }
}
#endif
