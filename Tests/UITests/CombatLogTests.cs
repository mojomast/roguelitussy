using Godotussy;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class CombatLogTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.CombatLog enemy action uses danger red", EnemyActionUsesDangerRed);
        registry.Add("UI.CombatLog loot uses bright gold", LootUsesBrightGold);
        registry.Add("UI.CombatLog critical is bold", CriticalUsesBold);
        registry.Add("UI.CombatLog old messages fade", OldMessagesFade);
        registry.Add("UI.EventBus empty log messages are safe", EmptyLogMessagesAreSafe);
    }

    private static void EnemyActionUsesDangerRed()
    {
        var markup = CombatLog.FormatEntryForTest("Goblin hits Rook.", LogCategory.EnemyAction, 0);

        Expect.True(markup.Contains(UiStyle.ToHex(UiStyle.DangerRed()), System.StringComparison.Ordinal),
            "Enemy log entries should use danger red.");
    }

    private static void LootUsesBrightGold()
    {
        var markup = CombatLog.FormatEntryForTest("Loot found: gold.", LogCategory.Loot, 0);

        Expect.True(markup.Contains(UiStyle.ToHex(UiStyle.BrightGold()), System.StringComparison.Ordinal),
            "Loot log entries should use bright gold.");
    }

    private static void CriticalUsesBold()
    {
        var markup = CombatLog.FormatEntryForTest("Rook dies.", LogCategory.Critical, 0);

        Expect.True(markup.Contains("[b]", System.StringComparison.Ordinal),
            "Critical log entries should be bold.");
    }

    private static void OldMessagesFade()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        for (var i = 0; i < 8; i++)
        {
            bus.EmitLogMessage($"message {i}");
        }

        Expect.True(log.RenderedText.Contains("99", System.StringComparison.Ordinal)
            || log.RenderedText.Contains("4d", System.StringComparison.Ordinal),
            "Older rendered log lines should include an alpha fade in their BBCode color.");
    }

    private static void EmptyLogMessagesAreSafe()
    {
        var bus = new EventBus();
        var log = new CombatLog();
        log.Bind(null, bus);

        bus.EmitLogMessage(null!);
        bus.EmitLogMessage(string.Empty);

        Expect.True(log.RenderedText is not null, "Null and empty messages should not throw during formatting.");
    }
}
