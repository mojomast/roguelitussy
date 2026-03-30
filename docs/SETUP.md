# Setup

This project targets .NET 8 and is structured so most development workflows can run without a local Godot editor runtime. You only need Godot itself when you want to open scenes, inspect editor tools, or run the playable shell.

## Requirements

- .NET 8 SDK
- Godot 4.4 with C# support for editor/runtime work
- Windows PowerShell or another shell capable of running `dotnet`
- A C#-capable editor such as VS Code or Visual Studio

## First-Time Repository Setup

1. Clone the repository.
2. Open the repository root.
3. Restore and build:

   ```powershell
   dotnet build godotussy.sln
   ```

4. Run the test suite to confirm the local environment is healthy:

   ```powershell
   dotnet run --project Tests/godotussy.Tests.csproj
   ```

## Opening The Project In Godot

1. Launch Godot 4.4.
2. Import or open `project.godot` from the repository root.
3. Confirm these autoloads are present:
   - `GameManager`
   - `EventBus`
   - `ContentDatabase`
4. Open `Scenes/Main.tscn` if you want to inspect the shell scene directly.

The project starts from `Scenes/Main.tscn`, which hosts `WorldRoot` and `UiRoot`. Most runtime services are attached through the autoloads rather than through a large scene tree.

## Editor Plugin

The repository includes the `Roguelike Tools` editor plugin at `Addons/roguelike_tools/`.

If the plugin is not already enabled in the editor:

1. Open Project Settings.
2. Go to Plugins.
3. Enable `Roguelike Tools`.

Once enabled, it adds project tools to the bottom panel.

## Build And Validation Commands

Build the main solution:

```powershell
dotnet build godotussy.sln
```

Run the full custom test harness:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj
```

Run the rendering-focused test compile profile:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:RenderingValidation=true
```

## Why The Project Builds Without Godot

`Compat/Godot/GodotStubs.cs` provides compile-time stand-ins for the Godot types used by the C# scripts. This allows the solution and tests to build in a standard .NET environment while keeping the pure simulation layer isolated from engine dependencies.

## Common Setup Pitfalls

- Do not leave temporary `.cs` files in the repository root. The SDK project includes them automatically and they can break the build.
- Use Godot 4.4 when opening the project. The project file declares `4.4` as a feature target.
- If you add new Godot API usage to scripts, update the compatibility stubs when necessary so CI-style .NET builds remain healthy.