using System;
using System.IO;
using Godot;

namespace Roguelike.Tests.TestFramework;

public sealed class TestRegistryTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("TestFramework.Failing test output includes full exception details", FailingTestOutputIncludesFullExceptionDetails);
        registry.Add("TestFramework.Empty filter runs all registered tests", EmptyFilterRunsAllRegisteredTests);
        registry.Add("TestFramework.Filter runs only matching tests", FilterRunsOnlyMatchingTests);
        registry.Add("TestFramework.Nonmatching filter executes zero tests and succeeds", NonmatchingFilterExecutesZeroTestsAndSucceeds);
        registry.Add("TestFramework.Registry resets Godot stub state between tests", RegistryResetsGodotStubStateBetweenTests);
    }

    private static void FailingTestOutputIncludesFullExceptionDetails()
    {
        var registry = new TestRegistry();
        registry.Add("TestFramework.Intentional failure", ThrowIntentionalFailure);

        using var output = new StringWriter();
        var result = registry.RunAll(output: output);
        var text = output.ToString();

        Expect.Equal(1, result, "A failing registry run should return a non-zero exit code");
        Expect.True(text.Contains("--- FAIL TestFramework.Intentional failure ---", StringComparison.Ordinal), "Failure output should include the failing test name");
        Expect.True(text.Contains("System.InvalidOperationException: intentional failure message", StringComparison.Ordinal), "Failure output should include exception type and message");
        Expect.True(text.Contains(nameof(ThrowIntentionalFailure), StringComparison.Ordinal), "Failure output should include stack-trace-like method content");
    }

    private static void EmptyFilterRunsAllRegisteredTests()
    {
        var registry = new TestRegistry();
        var executed = 0;
        registry.Add("TestFramework.Alpha", () => executed++);
        registry.Add("TestFramework.Beta", () => executed++);

        using var output = new StringWriter();
        var result = registry.RunAll(string.Empty, output);

        Expect.Equal(0, result, "An empty-filter run should succeed when all tests pass");
        Expect.Equal(2, executed, "An empty filter should run every registered test");
        Expect.True(output.ToString().Contains("Executed 2 of 2 registered tests. Skipped 0. Failures: 0.", StringComparison.Ordinal), "Summary should report all tests executed");
    }

    private static void FilterRunsOnlyMatchingTests()
    {
        var registry = new TestRegistry();
        var alphaRuns = 0;
        var betaRuns = 0;
        registry.Add("TestFramework.Alpha", () => alphaRuns++);
        registry.Add("TestFramework.Beta", () => betaRuns++);

        using var output = new StringWriter();
        var result = registry.RunAll("alpha", output);

        Expect.Equal(0, result, "A matching-filter run should succeed when selected tests pass");
        Expect.Equal(1, alphaRuns, "Filter should run matching tests case-insensitively");
        Expect.Equal(0, betaRuns, "Filter should skip nonmatching tests");
        Expect.True(output.ToString().Contains("Executed 1 of 2 registered tests. Skipped 1. Failures: 0.", StringComparison.Ordinal), "Summary should report filtered execution counts");
    }

    private static void NonmatchingFilterExecutesZeroTestsAndSucceeds()
    {
        var registry = new TestRegistry();
        var executed = 0;
        registry.Add("TestFramework.Alpha", () => executed++);

        using var output = new StringWriter();
        var result = registry.RunAll("does-not-match", output);

        Expect.Equal(0, result, "A nonmatching filter should return success");
        Expect.Equal(0, executed, "A nonmatching filter should execute zero tests");
        Expect.True(output.ToString().Contains("Executed 0 of 1 registered tests. Skipped 1. Failures: 0.", StringComparison.Ordinal), "Summary should clearly report zero executed tests");
    }

    private static void RegistryResetsGodotStubStateBetweenTests()
    {
        var registry = new TestRegistry();
        registry.Add("TestFramework.Mutate stub state", MutateStubState);
        registry.Add("TestFramework.Observe clean stub state", ObserveCleanStubState);

        using var output = new StringWriter();
        var result = registry.RunAll(output: output);

        Expect.Equal(0, result, "Registry should reset mutable Godot stub state before each test");
    }

    private static void ThrowIntentionalFailure()
    {
        throw new InvalidOperationException("intentional failure message");
    }

    private static void MutateStubState()
    {
        GD.MissingResourcePaths.Add("res://missing-resource.png");
        Image.MissingImagePaths.Add("missing-image.png");
        Input.SetMouseButtonPressed(MouseButton.Left, true);

        var viewport = new Node().GetViewport();
        viewport.Size = new Vector2(42f, 24f);
        viewport.SetInputAsHandled();
    }

    private static void ObserveCleanStubState()
    {
        Expect.Equal(0, GD.MissingResourcePaths.Count, "Missing resource paths should reset between tests");
        Expect.Equal(0, Image.MissingImagePaths.Count, "Missing image paths should reset between tests");
        Expect.False(Input.IsMouseButtonPressed(MouseButton.Left), "Pressed mouse buttons should reset between tests");

        var viewport = new Node().GetViewport();
        Expect.Equal(new Vector2(1280f, 720f), viewport.Size, "Shared viewport size should reset to its default");
        Expect.False(viewport.InputHandled, "Shared viewport input-handled flag should reset between tests");
    }
}
