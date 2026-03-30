# Tools

The repository includes both editor-time tools and in-game debug surfaces.

## Godot Editor Plugin

The `Roguelike Tools` plugin lives in `Addons/roguelike_tools/`.

When enabled, `RoguelikeToolsPlugin` adds two bottom-panel tools:

- `Map Editor`
- `Content Editor`

These are instantiated from:

- `Scripts/Tools/MapEditor.cs`
- `Scripts/Tools/ItemEditor.cs`

Use the plugin when editing or validating project content from within Godot.

## Tool Scenes

Tool scenes live under `Scenes/Tools/`:

- `DebugConsole.tscn`
- `DebugOverlay.tscn`
- `ItemEditor.tscn`
- `MapEditor.tscn`

## In-Game Debug Surfaces

`Scripts/UI/UIRoot.cs` wires the gameplay shell to the debug tools.

Current built-in shortcuts:

- Backquote toggles the debug console.
- `Q` toggles the debug overlay.

The exact command set for the debug console is owned by the tool scripts and their command processor.

## Tool Path Resolution

`Scripts/Tools/ToolPaths.cs` resolves the active content directory. It either accepts an explicit preferred directory or locates the repository content folder automatically through `ContentLoader.FindContentDirectory(...)`.

This means tool workflows assume the standard repository layout and the presence of all required `Content/*.json` files.

## When To Add A Tool

Add or extend a project tool when the task is editor- or debugging-focused and does not belong in the runtime player experience. Typical examples:

- content inspection or editing
- map inspection
- debug commands
- overlays for runtime diagnostics

Keep gameplay-facing menus and widgets in `Scripts/UI/`, not in the tools area.