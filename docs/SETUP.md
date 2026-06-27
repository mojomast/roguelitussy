# Setup

This project targets .NET 8 and is structured so most development workflows can run without a local Godot editor runtime. You only need Godot itself when you want to open scenes, inspect editor tools, or run the playable shell.

Once the game shell is running, the title screen and pause menu both expose an in-app developer workshop so you can create room drafts and scaffold content without opening the Godot editor.

## Requirements

- .NET 8 SDK
- Godot 4.5.2 Mono/.NET for editor/runtime work
- Windows PowerShell or another shell capable of running `dotnet`
- A C#-capable editor such as VS Code or Visual Studio

If .NET is not installed system-wide, a user-local SDK install under `$HOME/.dotnet` is sufficient. Export `DOTNET_ROOT=$HOME/.dotnet` and prepend `$HOME/.dotnet` to `PATH` for the current shell before running `dotnet` commands.

On Linux, Godot 4.5.2 Mono can also be installed user-locally by downloading the official `Godot_v4.5.2-stable_mono_linux_x86_64.zip`, extracting it under `$HOME/.local`, and linking the executable as `$HOME/.local/bin/godot`. Prepend `$HOME/.local/bin` to `PATH` before running headless Godot checks.

## First-Time Repository Setup

1. Clone the repository.
2. Open the repository root.
3. Restore and build the editorless stub profile:

   ```powershell
   dotnet build godotussy.csproj -p:UseGodotStubs=true
   ```

4. Run the test suite to confirm the local environment is healthy:

   ```powershell
   dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   ```

## Opening The Project In Godot

1. Launch Godot 4.5.2 Mono/.NET.
2. Import or open `project.godot` from the repository root.
3. Confirm these autoloads are present:
   - `GameManager`
   - `EventBus`
   - `ContentDatabase`
4. Open `Scenes/Main.tscn` if you want to inspect the shell scene directly.

In a fresh headless environment, run an editor import once before the plain startup smoke if imported `.ctex` resources are missing:

```powershell
godot --headless --editor --path . --quit
godot --headless --path . --quit
```

The project starts from `Scenes/Main.tscn`, which hosts `WorldRoot` and `UiRoot`. Most runtime services are attached through the autoloads rather than through a large scene tree.

CI runs both the editorless .NET stub profile and a real Godot 4.5.2 Mono headless import/startup smoke. The .NET job restores the solution, verifies formatting with `dotnet format --verify-no-changes`, validates JSON syntax for all files under `Content/`, treats warnings as errors on compile steps through `-p:RoguelitussyWarningsAsErrors=true`, and caches NuGet packages. Keep `.godot/`, `.mono/`, `bin/`, and `obj/` out of commits; Godot import products are regenerated from committed source assets and `.import` files.

When adding or renaming SVG/PNG source art under `Assets/`, run the headless editor import before the startup smoke so Godot can regenerate local import products from committed assets.

## Using The In-App Developer Workshop

Launch the game shell and open `Dev Tools` from the title screen or pause menu, or press `T` during gameplay.

The workshop currently supports four runtime tabs:

- `Rooms` for creating, loading, previewing, editing, validating, saving, and playtesting room prefab drafts
- `Items` for scaffolding and tuning item templates before saving `items.json`, then dropping the selected runtime item into the current run
- `Enemies` for scaffolding and tuning enemy templates before saving `enemies.json`, then spawning the selected enemy near the player
- `Commands` for seeded run control, save/load slot management, heal/reveal runtime helpers, floor travel, player teleport, reloading tool data, reloading runtime content from disk, validating content, and jumping into the debug console

Core controls inside the workshop:

- `Tab` switches tabs
- `Up` and `Down` select a field or action
- `Left` and `Right` or `+` and `-` adjust the selected field
- `Enter` applies the selected action
- `Esc` or `T` closes the workshop

If you are iterating on gameplay content, the usual flow is save the draft, reload runtime content from the `Commands` tab, and then use room playtest or the item/enemy runtime actions to verify the result immediately. The same tab now also covers quick seeded restart, save/load slot checks, full-heal recovery, floor jumps, teleporting, and map reveal/reset when you want to keep iteration inside the running app.

## Editor Plugin

The repository includes the `Roguelike Tools` editor plugin at `Addons/roguelike_tools/`.

If the plugin is not already enabled in the editor:

1. Open Project Settings.
2. Go to Plugins.
3. Enable `Roguelike Tools`.

Once enabled, it adds project tools to the bottom panel.

The editor plugin is still useful for scene-centric workflows, but it is no longer required for basic room and content authoring.

## Build And Validation Commands

Build the main project using the Godot compatibility stubs:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true
```

Build the solution, including the custom test project for IDE/structural coverage:

```powershell
dotnet build godotussy.sln
```

Verify formatting (CI enforces this):

```powershell
dotnet format --verify-no-changes godotussy.sln
```

Run the warnings-as-errors compile checks used by CI:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true -p:RoguelitussyWarningsAsErrors=true
dotnet build Tests/godotussy.Tests.csproj -p:RoguelitussyWarningsAsErrors=true
```

Validate JSON content syntax locally (CI runs the same check):

```powershell
Get-ChildItem -Recurse -Filter *.json -Path Content | ForEach-Object { python3 -m json.tool $_.FullName > $null }
```

Build the test project:

```powershell
dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run the full custom test harness:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run a focused subset while iterating:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -- --filter Simulation.
```

Run the rendering-focused test compile profile:

```powershell
dotnet restore godotussy.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
```

This profile removes persistence implementation files from the main project and skips persistence-dependent test files. `GameManager` uses a no-op save manager only under this compile symbol so rendering and UI validation can run without the save subsystem.

## Why The Project Builds Without Godot

`Compat/Godot/GodotStubs.cs` provides compile-time stand-ins for the Godot types used by the C# scripts. This allows the solution and tests to build in a standard .NET environment while keeping the pure simulation layer isolated from engine dependencies.

## Common Setup Pitfalls

- Do not leave temporary `.cs` files in the repository root. The SDK project includes them automatically and they can break the build.
- Use Godot 4.5.2 Mono/.NET when opening the project. The SDK and project feature flag are pinned to the 4.5 line.
- If you add new Godot API usage to scripts, update the compatibility stubs when necessary so CI-style .NET builds remain healthy.
