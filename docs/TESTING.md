# Testing

The project uses a custom .NET test harness instead of xUnit or NUnit.

## How Tests Run

`Tests/Program.cs` reflects over the test assembly, instantiates every concrete `ITestSuite`, registers its tests with `TestRegistry`, and executes them.

`TestRegistry` prints a simple pass/fail line per test and returns a non-zero exit code when any test fails.

## Standard Commands

Run the full suite:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj
```

Build everything before a larger change set:

```powershell
dotnet build godotussy.sln
```

Run the rendering-focused compile profile:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:RenderingValidation=true
```

## Test Areas

The repository contains targeted suites for:

- AI
- content loading and validation
- generation
- integration flows
- persistence
- rendering
- simulation
- UI
- architecture smoke coverage

## Writing New Tests

1. Add a class that implements `ITestSuite`.
2. Register each test through the provided registry.
3. Keep tests deterministic and self-contained.
4. Prefer focused tests around one subsystem or regression.

When changing simulation, persistence, generation, or content loading, add or update tests in the same change.

## Testing Guidance By Change Type

- Simulation/action changes: add simulation or integration tests.
- Save-format or migration changes: add persistence serialization and migration coverage.
- Content loader changes: add content tests and at least one failure-path case when practical.
- Rendering or UI changes: add rendering or UI tests where the behavior is covered by the stubbed environment.

## Determinism Expectations

Tests should avoid hidden time, file-system, or random-number dependencies unless the behavior under test is explicitly about those concerns. If randomness is involved, drive it from a known seed.