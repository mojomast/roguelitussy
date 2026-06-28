using Godot;
using Godotussy;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class FloorSummaryTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.FloorSummary opens populated stats", OpensPopulatedStats);
        registry.Add("UI.FloorSummary body includes key stats", BodyIncludesKeyStats);
        registry.Add("UI.FloorSummary body renders BBCode in rich text", BodyRendersBbcodeInRichText);
        registry.Add("UI.FloorSummary perfect floor flavor", PerfectFloorFlavor);
        registry.Add("UI.FloorSummary bloodbath flavor", BloodbathFlavor);
        registry.Add("UI.FloorSummary timer starts at six seconds", TimerStartsAtSixSeconds);
        registry.Add("UI.FloorSummary any key engages and stops countdown", AnyKeyEngagesAndStopsCountdown);
        registry.Add("UI.FloorSummary enter emits confirmed", EnterEmitsConfirmed);
        registry.Add("UI.FloorSummary escape emits confirmed", EscapeEmitsConfirmed);
        registry.Add("UI.FloorSummary timer expiry emits confirmed", TimerExpiryEmitsConfirmed);
    }

    private static FloorStats SampleStats(int enemiesKilled = 3, int damageTaken = 5, int trapsTriggered = 1) => new(
        2,
        enemiesKilled,
        4,
        75,
        damageTaken,
        37,
        1,
        trapsTriggered);

    private static void OpensPopulatedStats()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats());

        Expect.True(screen.Visible, "Opening with floor stats should show the summary.");
        Expect.True(screen.BuildBodyMarkup().Contains("Floor 2"), "Summary body should include floor number.");
    }

    private static void BodyIncludesKeyStats()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats());
        var markup = screen.BuildBodyMarkup();

        Expect.True(markup.Contains("Floor 2"), "Markup should contain floor number.");
        Expect.True(markup.Contains("Enemies slain"), "Markup should contain enemy label.");
        Expect.True(markup.Contains("3"), "Markup should contain enemy count.");
        Expect.True(markup.Contains("Turns spent"), "Markup should contain turns label.");
        Expect.True(markup.Contains("37"), "Markup should contain turns spent.");
    }

    private static void BodyRendersBbcodeInRichText()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats());

        var body = FindChild<RichTextLabel>(screen, "Label");

        Expect.NotNull(body, "Floor summary body should use a RichTextLabel.");
        Expect.True(body!.BbcodeEnabled, "Floor summary body should parse BBCode instead of displaying color tags literally.");
        Expect.True(body.Text.Contains("[color=", System.StringComparison.Ordinal), "Floor summary body text should retain authored BBCode markup for RichTextLabel parsing.");
    }

    private static void PerfectFloorFlavor()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats(damageTaken: 0, trapsTriggered: 0));

        Expect.True(screen.BuildBodyMarkup().Contains("Perfect floor."), "No-damage floor should use perfect-floor flavor.");
    }

    private static void BloodbathFlavor()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats(enemiesKilled: 12, damageTaken: 1, trapsTriggered: 0));

        Expect.True(screen.BuildBodyMarkup().Contains("A bloodbath."), "High-kill floor should use bloodbath flavor.");
    }

    private static void TimerStartsAtSixSeconds()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats());

        Expect.Equal(6.0f, screen.CountdownSeconds, "Summary countdown should start at six seconds.");
    }

    private static void AnyKeyEngagesAndStopsCountdown()
    {
        var screen = new FloorSummaryUI();
        screen.Open(SampleStats());

        var handled = screen.HandleKey(Key.A);
        screen._Process(2.0);

        Expect.True(handled, "Non-confirm keys should be handled while the summary is visible.");
        Expect.True(screen.PlayerEngaged, "Any key should mark the player as engaged.");
        Expect.Equal(6.0f, screen.CountdownSeconds, "Engaged summary should stop countdown.");
        Expect.True(screen.Visible, "Non-confirm keys should not close the summary.");
    }

    private static void EnterEmitsConfirmed()
    {
        var bus = new EventBus();
        var screen = new FloorSummaryUI();
        var count = 0;
        bus.FloorTransitionConfirmed += () => count++;
        screen.Bind(bus);
        screen.Open(SampleStats());

        var handled = screen.HandleKey(Key.Enter);

        Expect.True(handled, "Enter should be handled by the floor summary.");
        Expect.Equal(1, count, "Enter should emit exactly one transition confirmation.");
        Expect.False(screen.Visible, "Enter should close the summary.");
    }

    private static void EscapeEmitsConfirmed()
    {
        var bus = new EventBus();
        var screen = new FloorSummaryUI();
        var count = 0;
        bus.FloorTransitionConfirmed += () => count++;
        screen.Bind(bus);
        screen.Open(SampleStats());

        var handled = screen.HandleKey(Key.Escape);

        Expect.True(handled, "Escape should be handled by the floor summary.");
        Expect.Equal(1, count, "Escape should emit exactly one transition confirmation.");
        Expect.False(screen.Visible, "Escape should close the summary.");
    }

    private static void TimerExpiryEmitsConfirmed()
    {
        var bus = new EventBus();
        var screen = new FloorSummaryUI();
        var count = 0;
        bus.FloorTransitionConfirmed += () => count++;
        screen.Bind(bus);
        screen.Open(SampleStats());

        screen._Process(6.1);

        Expect.Equal(1, count, "Timer expiry should emit exactly one transition confirmation.");
        Expect.False(screen.Visible, "Timer expiry should close the summary.");
    }

    private static T? FindChild<T>(Node node, string name) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match && string.Equals(child.Name, name, System.StringComparison.Ordinal))
            {
                return match;
            }

            var nested = FindChild<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
