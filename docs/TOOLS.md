# Tools

The repository now supports two parallel tooling paths:

- Godot editor tools for scene-oriented workflows
- Runtime tools inside the playable shell for authoring and validation without opening the editor

The runtime path is the default recommendation when you just want to build content and iterate on the foundation quickly.

## In-App Developer Workshop

`Scripts/Tools/DevToolsWorkbench.cs` is wired into `Scripts/UI/UIRoot.cs` and is available from:

- the title screen via `Dev Tools`
- the pause menu via `Dev Tools`
- gameplay via `T`

When opened from the title flow, the workshop temporarily dismisses the main menu and restores it on close so the workshop remains the only active full-screen overlay.

The workshop is a menu-driven runtime shell around the lower-level tool backends:

- `Rooms` uses `Scripts/Tools/MapEditor.cs` to create, load, preview, validate, save, and immediately playtest room prefab drafts
- `Items` uses `Scripts/Tools/ItemEditor.cs` to scaffold and tune item templates before saving `items.json`, and can drop the selected runtime item into the current run
- `Enemies` uses `Scripts/Tools/ItemEditor.cs` to scaffold and tune enemy templates before saving `enemies.json`, and can spawn the selected enemy near the player for iteration
- `Commands` starts seeded expeditions, saves and loads slots, heals, reveals, travels floors, teleports the player, reloads tool/runtime data, validates content, and hands off to the debug console when freeform commands are more efficient

Workshop controls:

- `Tab` switches tabs
- `Up` and `Down` select a field or action
- `Left` and `Right` or `+` and `-` adjust the selected field
- `Enter` applies the selected action
- `Esc` or `T` closes the workshop

Use this workflow when you want to extend the content foundation without touching Godot editor panels.

The intended fast loop is:

1. Author or adjust a room, item, or enemy draft in the workshop.
2. Save the relevant JSON document.
3. Use `Reload runtime content from disk` when you want the active game session to pick up the new content definitions.
4. Use room playtest or the item/enemy spawn actions to validate behavior immediately in the running game.
5. Use the `Commands` tab when you need to start a fresh seeded run, save/load a slot, heal quickly, reveal the map, jump floors, or teleport within the active run without dropping to console commands.

## Godot Editor Plugin

The `Roguelike Tools` plugin lives in `Addons/roguelike_tools/`.

When enabled, `RoguelikeToolsPlugin` adds two bottom-panel tools:

- `Map Editor`
- `Content Editor`

These are instantiated from:

- `Scripts/Tools/MapEditor.cs`
- `Scripts/Tools/ItemEditor.cs`

Use the plugin when editing or validating project content from within Godot.

The plugin and the runtime workshop share the same backend tool scripts. They are different surfaces over the same content-authoring logic, not separate implementations.

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
- `T` toggles the developer workshop.

The exact command set for the debug console is owned by the tool scripts and their command processor.

The debug console is still the best path for one-off freeform commands such as door toggles, inventory inspection, or command discovery. The workshop now covers the repeatable runtime iteration actions like run control, teleporting, floor travel, spawning selected content, and visibility management.

## Tool Path Resolution

`Scripts/Tools/ToolPaths.cs` resolves the active content directory. It either accepts an explicit preferred directory or locates the repository content folder automatically through `ContentLoader.FindContentDirectory(...)`.

This means tool workflows assume the standard repository layout and the presence of all required `Content/*.json` files.

## When To Add A Tool

Add or extend a project tool when the task is editor- or debugging-focused and does not belong in the runtime player experience. Typical examples:

- content inspection or editing
- map inspection
- debug commands
- overlays for runtime diagnostics

If the tool is meant to support building the game out from the foundation without opening Godot, prefer exposing it through the runtime workshop as well as through any editor-facing plugin hooks.

Keep gameplay-facing menus and widgets in `Scripts/UI/`, not in the tools area.