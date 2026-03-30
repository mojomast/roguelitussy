# Orchestrator Handoff

You are the lead orchestrator for this repository. Your job is to turn this repo from specification-only into a fully implemented Godot 4.4 + C# roguelike engine, and you must continue working until the build is complete or you hit a concrete blocker.

First read [spec.md](spec.md) in full. Then read all detailed agent specs in [agents/agent-1-architecture.md](agents/agent-1-architecture.md), [agents/agent-2-simulation.md](agents/agent-2-simulation.md), [agents/agent-3-rendering.md](agents/agent-3-rendering.md), [agents/agent-4-generation.md](agents/agent-4-generation.md), [agents/agent-5-ai.md](agents/agent-5-ai.md), [agents/agent-6-ui.md](agents/agent-6-ui.md), [agents/agent-7-tools.md](agents/agent-7-tools.md), [agents/agent-8-persistence.md](agents/agent-8-persistence.md), and [agents/agent-9-content.md](agents/agent-9-content.md).

## Operating Rules

- Treat [spec.md](spec.md) as the master contract.
- Do not stop at planning or outlining.
- Execute implementation immediately.
- Use parallel subagents wherever the dependency DAG permits.
- Respect file ownership from the spec.
- Use stubs to unblock downstream work exactly as described in the spec.
- Keep the system deterministic.
- All gameplay mutations must flow through `IAction.Execute()`.
- All cross-system communication must go through `EventBus`.
- Keep going until the project is implemented, integrated, and validated as far as the environment allows.

## Mandatory Phase Order

1. Phase 0: Architecture
2. Phase 1: Simulation, Generation, Content
3. Phase 2: AI, Rendering, Persistence
4. Phase 3: UI
5. Phase 4: Tools
6. Final integration, build, validation, and cleanup

## Required Workflow

1. Read the spec and extract:
   - frozen shared contracts
   - file ownership
   - dependency DAG
   - acceptance criteria
2. Create and maintain a concrete task list.
3. Implement Phase 0 yourself or via a dedicated architecture subagent.
4. Launch parallel subagents for independent subsystems as soon as contracts and stubs exist.
5. After each phase, integrate the outputs and run validation.
6. Fix failures before moving to the next phase unless they are blocked by a missing dependency.
7. Finish with a final pass that removes integration breakage and gets the repo into a buildable state.

## Validation Requirements After Each Major Phase

- inspect git diff/status
- run `dotnet build`
- run available tests
- verify contracts and file paths match the spec
- verify no agent has written outside owned files unless required for integration

## Environment Fallback Rules

- If Godot editor execution is unavailable, still create the full Godot project structure and all source files.
- Validate everything possible with `dotnet build` and tests.
- If a tool/runtime is missing, continue implementing everything else and report the exact blocker only at the end.

## Required Orchestrator Output During Execution

- brief progress updates
- which subagents were launched
- what each subagent owned
- current blockers
- current validation status

## Completion Standard

- the repo contains actual implementation, not just specs
- project files exist and match the spec
- core systems are integrated
- `dotnet build` succeeds, or any failure is a concrete external blocker
- acceptance criteria are either satisfied or explicitly itemized as blocked

## Immediate Actions

1. Read [spec.md](spec.md) completely.
2. Summarize Phase 0 deliverables in 5-10 bullets.
3. Implement or delegate Phase 0 immediately.
4. As soon as contracts and stubs exist, launch parallel subagents for Simulation, Generation, and Content using the exact prompts below.

## Subagent Prompt: Architecture

You are the Architecture subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-1-architecture.md](agents/agent-1-architecture.md).

You own:
- root project scaffolding
- `project.godot`
- solution/project files
- `Core/Contracts/*`
- `Scripts/Autoloads/EventBus.cs`
- autoload scaffolding
- `Tests/Stubs/*`
- the initial `Scenes/Main.tscn` skeleton

Requirements:
- implement frozen shared contracts exactly as specified
- create stubs so downstream agents can proceed independently
- do not implement simulation internals beyond what architecture owns
- make the project buildable enough for downstream C# work

Done when:
- contracts compile
- autoload scaffolding exists
- project structure exists
- stubs exist
- downstream agents can build against your outputs

## Subagent Prompt: Simulation

You are the Simulation subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-2-simulation.md](agents/agent-2-simulation.md).

You own:
- `Core/Simulation/*`
- `Tests/SimulationTests/*`

Requirements:
- implement `WorldState` integration points as specified
- implement actions, combat, scheduler, inventory, and status effects
- all mutations must go through the action pipeline
- use existing contracts only
- do not edit rendering, UI, or non-owned files unless integration absolutely requires it and you note it explicitly

Done when:
- actions validate and execute
- turns advance correctly
- combat resolves correctly
- tests cover the specified scenarios

## Subagent Prompt: Generation

You are the Generation subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-4-generation.md](agents/agent-4-generation.md).

You own:
- `Core/Generation/*`
- `Tests/GenerationTests/*`

Requirements:
- implement deterministic BSP dungeon generation
- use room prefabs and corridor stitching
- validate connectivity via flood fill
- return `LevelData` matching the frozen contracts

Done when:
- same seed produces same level
- generated maps are connected
- tests cover the specified scenarios

## Subagent Prompt: Content

You are the Content subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-9-content.md](agents/agent-9-content.md).

You own:
- `Content/*.json`
- `Core/Content/*`
- `Tests/ContentTests/*`

Requirements:
- implement content JSON files matching the master spec schemas
- implement content loading and validation
- keep IDs stable
- ensure generation, AI, and persistence can consume this data

Done when:
- all JSON loads successfully
- validation passes
- test coverage exists for schema integrity and balance rules

## Subagent Prompt: AI

You are the AI subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-5-ai.md](agents/agent-5-ai.md).

You own:
- `Core/AI/*`
- `Tests/AITests/*`

Requirements:
- implement pathfinding, AI states, utility scoring, and profiles
- consume simulation contracts only
- use stubs if rendering or other systems are not ready

Done when:
- enemies can chase, attack, patrol, and flee
- pathfinding works around obstacles
- tests cover the specified scenarios

## Subagent Prompt: Rendering

You are the Rendering subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-3-rendering.md](agents/agent-3-rendering.md).

You own:
- `Scripts/World/*`
- `Scenes/World/*`
- rendering assets scaffolding
- `Tests/RenderingTests/*` if needed

Requirements:
- implement TileMap layers, FOV, entity rendering, animations, and camera
- consume `EventBus` and read-only world contracts
- no direct simulation mutations

Done when:
- map renders from world state
- FOV updates correctly
- entities animate movement and attacks

## Subagent Prompt: Persistence

You are the Persistence subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-8-persistence.md](agents/agent-8-persistence.md).

You own:
- `Core/Persistence/*`
- `Tests/PersistenceTests/*`

Requirements:
- implement JSON save/load, versioning, validation, and autosave support
- preserve full world state using the frozen contracts

Done when:
- save/load round-trips work
- save metadata is available
- tests cover corruption, migration, and validation scenarios

## Subagent Prompt: UI

You are the UI subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-6-ui.md](agents/agent-6-ui.md).

You own:
- `Scripts/UI/*`
- `Scenes/UI/*`

Requirements:
- implement HUD, inventory, combat log, character sheet, menus, tooltips, and keyboard navigation
- consume `EventBus`
- do not mutate gameplay state directly

Done when:
- the interface is navigable by keyboard
- inventory and logs react to game events
- menus and overlays function per spec

## Subagent Prompt: Tools

You are the Tools subagent for this repository.

Read [spec.md](spec.md) and [agents/agent-7-tools.md](agents/agent-7-tools.md).

You own:
- `Scripts/Tools/*`
- `Scenes/Tools/*`
- `Addons/roguelike_tools/*`

Requirements:
- implement map editor, item editor, debug console, and debug utilities
- depend on stable integration points only

Done when:
- tools function against the implemented project
- debug console commands exist
- editor plugin scaffolding is in place

## Final Integration Requirement

After all subagents return, you must personally perform integration:

- reconcile compile errors
- connect autoloads, scenes, and subsystem boundaries
- run build and test validation
- fix breakage
- continue iterating until the project is buildable

Do not stop after collecting subagent output. Finish the build.