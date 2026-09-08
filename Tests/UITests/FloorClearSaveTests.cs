using System;
using System.IO;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class FloorClearSaveTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        // The rendering-only profile excludes disk persistence from the main assembly.
        if (typeof(WorldState).Assembly.GetType("Roguelike.Core.SaveManager") is null) return;
        registry.Add("FloorClear.UI fresh manager does not repay a cleared saved floor", FreshManager);
        registry.Add("FloorClear.UI loading pre-clear save replaces stale reward state", PreClearReload);
        registry.Add("FloorClear.UI cached floor reward survives save and revisit", CachedRevisit);
    }

    private static GameManager Manager(ISaveManager saves, EventBus bus)
    {
        var manager = new GameManager();
        manager.AttachServices(new WorldState(), new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), saves, bus);
        return manager;
    }

    private static void Clear(GameManager manager)
    {
        foreach (var enemy in manager.World!.Entities.Where(entity => entity.Faction == Faction.Enemy).ToArray())
            manager.World.RemoveEntity(enemy.Id);
        Expect.Equal(ActionResult.Success, manager.ProcessPlayerAction(new WaitAction(manager.World.Player.Id)).Result, "Wait should resolve the cleared floor");
    }

    private static int Gold(GameManager manager) => manager.World!.Player.GetComponent<WalletComponent>()!.Gold;

    private static void FreshManager() => WithSave(saves =>
    {
        var original = Manager(saves, new EventBus());
        original.StartNewGame(42);
        Clear(original);
        Expect.True(original.SaveToSlot(1), "Cleared floor should save");
        var bus = new EventBus();
        var events = 0;
        bus.FloorCleared += _ => events++;
        var loaded = Manager(saves, bus);
        Expect.True(loaded.LoadFromSlot(1), "Fresh manager should load");
        var gold = Gold(loaded);
        Clear(loaded);
        Expect.Equal(gold, Gold(loaded), "Saved cleared floor must not pay again");
        Expect.Equal(0, events, "Saved cleared floor must not emit another clear event");
    });

    private static void PreClearReload() => WithSave(saves =>
    {
        var bus = new EventBus();
        var events = 0;
        bus.FloorCleared += _ => events++;
        var manager = Manager(saves, bus);
        manager.StartNewGame(42);
        var gold = Gold(manager);
        Expect.True(manager.SaveToSlot(1), "Pre-clear floor should save");
        Clear(manager);
        Expect.Equal(gold + 10, Gold(manager), "Initial clear should pay once");
        Expect.True(manager.LoadFromSlot(1), "Same manager should reload earlier snapshot");
        Expect.True(manager.World!.Entities.Any(entity => entity.Faction == Faction.Enemy), "Earlier snapshot should restore enemies");
        Clear(manager);
        Clear(manager);
        Expect.Equal(gold + 10, Gold(manager), "Earlier snapshot should become eligible again, exactly once");
        Expect.Equal(2, events, "Each timeline should award once");
    });

    private static void CachedRevisit() => WithSave(saves =>
    {
        var original = Manager(saves, new EventBus());
        original.StartNewGame(42);
        Clear(original);
        Expect.True(original.TravelToFloor(1), "Travel should cache rewarded floor");
        Expect.True(original.SaveToSlot(1), "Multi-floor run should save");
        var bus = new EventBus();
        var events = 0;
        bus.FloorCleared += _ => events++;
        var loaded = Manager(saves, bus);
        Expect.True(loaded.LoadFromSlot(1), "Multi-floor run should load");
        Expect.True(loaded.TravelToFloor(0), "Cached floor should be revisitable");
        var gold = Gold(loaded);
        Clear(loaded);
        Expect.Equal(gold, Gold(loaded), "Revisiting cached rewarded floor must not repay");
        Expect.Equal(0, events, "Revisit must not emit a duplicate clear event");
    });

    private static void WithSave(Action<ISaveManager> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "floor-clear-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var type = typeof(WorldState).Assembly.GetType("Roguelike.Core.SaveManager")!;
            test((ISaveManager)Activator.CreateInstance(type, new object?[] { directory, null })!);
        }
        finally { Directory.Delete(directory, true); }
    }
}
