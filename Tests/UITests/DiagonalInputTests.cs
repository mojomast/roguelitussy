using System.Collections.Generic;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class DiagonalInputTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Diagonal prefix completes all four diagonals in one turn", PrefixMovesAllDiagonals);
        registry.Add("UI.Diagonal prefix attacks without preparatory movement", PrefixAttacksAdjacentEnemy);
        registry.Add("UI.Diagonal direct and numpad keys submit one direction or wait", DirectKeysMoveAndWait);
        registry.Add("UI.Diagonal same-axis input replaces the partial direction", SameAxisUpdatesDirection);
        registry.Add("UI.Diagonal prefix cancellation spends no turn", CancellationSpendsNoTurn);
        registry.Add("UI.Diagonal non-direction commands cancel and open inventory", InventoryCancelsPrefix);
        registry.Add("UI.Diagonal input disable and rebind clear pending prefixes", DisableAndRebindClearPrefixes);
        registry.Add("UI.Diagonal world replacement clears pending prefixes", WorldReplacementClearsPrefixes);
        registry.Add("UI.Diagonal root ignores echoes and releases", RootIgnoresEchoesAndReleases);
        registry.Add("UI.Diagonal plain arrows keep exact cardinal destinations", PlainArrowsRemainCardinal);
        registry.Add("UI.Diagonal moves preserve corner restrictions", MovesPreserveCorners);
        registry.Add("UI.Diagonal direct keys do not expand run directions", RunRemainsCardinal);
        registry.Add("UI.Input ranged direction submits a ranged action without changing melee cardinal input", RangedDirectionSubmitsRangedAction);
    }

    private static void PrefixMovesAllDiagonals()
    {
        foreach (var (first, second, delta) in new[]
        {
            (Key.Up, Key.Left, new Position(-1, -1)),
            (Key.D, Key.W, new Position(1, -1)),
            (Key.A, Key.S, new Position(-1, 1)),
            (Key.Down, Key.Right, new Position(1, 1)),
        })
        {
            var context = CreateContext();
            Press(context.Root, Key.V);
            Press(context.Root, first);
            Expect.Equal(0, context.Actions.Count, "Choosing the prefix and first axis must not submit an action.");
            Expect.Equal(0, context.World.TurnNumber, "Partial diagonal selection must not spend a turn.");
            Press(context.Root, second);
            Expect.Equal(new Position(3, 3) + delta, context.World.Player.Position, "The two axes should combine into one move.");
            Expect.Equal(1, context.Actions.Count, "Completion must submit exactly one action.");
            Expect.Equal(1, context.World.TurnNumber, "Completion must process exactly one turn.");
        }
    }

    private static void PrefixAttacksAdjacentEnemy()
    {
        var context = CreateContext();
        var enemy = new StubEntity("Target", new Position(4, 2));
        context.World.AddEntity(enemy);
        Press(context.Root, Key.V);
        Press(context.Root, Key.Up);
        Expect.Equal(0, context.Actions.Count, "Selecting the first axis must not let the enemy respond.");
        Press(context.Root, Key.Right);
        Expect.Equal(1, context.Actions.Count, "Diagonal melee should submit one action.");
        Expect.True(context.Actions[0] is AttackAction attack && attack.TargetId == enemy.Id, "The existing attack action must target the diagonal enemy.");
        Expect.Equal(new Position(3, 3), context.World.Player.Position, "Melee must not reposition the player first.");
        var direct = CreateContext();
        var directEnemy = new StubEntity("Target", new Position(4, 2));
        direct.World.AddEntity(directEnemy);
        direct.Manager.ProcessPlayerAction(new AttackAction(direct.World.Player.Id, directEnemy.Id));
        Expect.Equal(direct.World.TurnNumber, context.World.TurnNumber, "Diagonal input must cost the same scheduler time as a direct melee action.");
    }

    private static void DirectKeysMoveAndWait()
    {
        foreach (var (key, delta) in new[]
        {
            (Key.Home, new Position(-1, -1)), (Key.Pageup, new Position(1, -1)),
            (Key.End, new Position(-1, 1)), (Key.Pagedown, new Position(1, 1)),
            (Key.Kp7, new Position(-1, -1)), (Key.Kp8, new Position(0, -1)),
            (Key.Kp9, new Position(1, -1)), (Key.Kp4, new Position(-1, 0)),
            (Key.Kp6, new Position(1, 0)), (Key.Kp1, new Position(-1, 1)),
            (Key.Kp2, new Position(0, 1)), (Key.Kp3, new Position(1, 1)),
            (Key.Kp5, Position.Zero),
        })
        {
            var context = CreateContext();
            Press(context.Root, key);
            Expect.Equal(new Position(3, 3) + delta, context.World.Player.Position, $"{key} should use its keypad direction.");
            Expect.Equal(1, context.Actions.Count, "Direct keys should submit exactly once.");
            Expect.Equal(1, context.World.TurnNumber, "Direct keys should spend one turn.");
            if (delta == Position.Zero)
            {
                Expect.True(context.Actions[0] is WaitAction, "Numpad 5 must wait.");
            }
        }
    }

    private static void SameAxisUpdatesDirection()
    {
        var context = CreateContext();
        foreach (var key in new[] { Key.V, Key.Up, Key.Up, Key.Down, Key.S })
        {
            Press(context.Root, key);
        }

        Expect.Equal(0, context.Actions.Count, "Repeated or opposite same-axis choices should not submit an action.");
        Expect.Equal(0, context.World.TurnNumber, "Updating the partial direction must not spend time.");
        Press(context.Root, Key.Right);
        Expect.Equal(new Position(4, 4), context.World.Player.Position, "The most recent same-axis direction should win.");
        Expect.Equal(1, context.Actions.Count, "Only the perpendicular choice should complete the action.");
    }

    private static void CancellationSpendsNoTurn()
    {
        foreach (var cancel in new[] { Key.V, Key.Escape })
        {
            var context = CreateContext();
            Press(context.Root, Key.V);
            Press(context.Root, Key.Up);
            Press(context.Root, cancel);
            Expect.Equal(0, context.World.TurnNumber, "Cancellation should be free.");
            Expect.Equal(0, context.Actions.Count, "Cancellation should not submit an action.");
            Expect.False(context.Root.PauseMenu.Visible, "Escape should cancel the prefix rather than open pause.");
            Press(context.Root, Key.Right);
            Expect.Equal(new Position(4, 3), context.World.Player.Position, "The next arrow should be an ordinary cardinal move.");
        }
    }

    private static void InventoryCancelsPrefix()
    {
        var context = CreateContext();
        Press(context.Root, Key.V);
        Press(context.Root, Key.Up);
        Press(context.Root, Key.I);
        Expect.True(context.Root.Inventory.Visible, "A non-direction command should still open inventory.");
        Press(context.Root, Key.Right);
        Expect.Equal(0, context.Actions.Count, "Inventory arrows must not complete gameplay prefixes.");
        Press(context.Root, Key.Escape);
        Press(context.Root, Key.Right);
        Expect.Equal(new Position(4, 3), context.World.Player.Position, "Closing inventory should not restore the old partial direction.");

        var waitContext = CreateContext();
        Press(waitContext.Root, Key.V);
        Press(waitContext.Root, Key.Up);
        Press(waitContext.Root, Key.Space);
        Expect.True(waitContext.Actions.Count == 1 && waitContext.Actions[0] is WaitAction, "A non-direction gameplay command should execute normally.");
        Press(waitContext.Root, Key.Right);
        Expect.Equal(new Position(4, 3), waitContext.World.Player.Position, "Wait must clear the prefix too.");
    }

    private static void DisableAndRebindClearPrefixes()
    {
        foreach (var prefix in new[] { Key.V, Key.R })
        {
            foreach (var rebind in new[] { false, true })
            {
                var context = CreateContext();
                Press(context.Root, prefix);
                if (prefix == Key.V)
                {
                    Press(context.Root, Key.Up);
                }

                if (rebind)
                {
                    context.Root.InputHandler.Bind(context.Manager, context.Bus);
                }
                else
                {
                    context.Root.InputHandler.SetInputEnabled(false);
                    Expect.False(context.Root.InputHandler.HandleKey(Key.Right), "Disabled gameplay must reject input.");
                    context.Root.InputHandler.SetInputEnabled(true);
                }

                Press(context.Root, Key.Right);
                Expect.Equal(new Position(4, 3), context.World.Player.Position, "Disable/rebind must clear both run and diagonal prefixes.");
                Expect.Equal(1, context.World.TurnNumber, "No buffered run or diagonal action should survive reset.");
            }
        }
    }

    private static void WorldReplacementClearsPrefixes()
    {
        foreach (var prefix in new[] { Key.V, Key.R })
        {
            var context = CreateContext();
            // Keep this handler independent of root rebind notifications to exercise identity detection itself.
            var input = new InputHandler();
            input.Bind(context.Manager, context.Bus);
            input.HandleKey(prefix);
            if (prefix == Key.V)
            {
                input.HandleKey(Key.Up);
            }

            var replacement = CreateWorld();
            context.Manager.LoadWorld(replacement);
            input.HandleKey(Key.Right);
            Expect.Equal(new Position(4, 3), replacement.Player.Position, "A replaced world must receive a fresh cardinal action.");
            Expect.Equal(1, replacement.TurnNumber, "World replacement must discard buffered run/diagonal state.");
            Expect.Equal(0, context.World.TurnNumber, "The previous world must remain unchanged.");
        }
    }

    private static void RootIgnoresEchoesAndReleases()
    {
        var context = CreateContext();
        Press(context.Root, Key.V);
        Press(context.Root, Key.Up);
        context.Root._UnhandledInput(new InputEventKey { Pressed = true, Echo = true, PhysicalKeycode = Key.Right });
        context.Root._UnhandledInput(new InputEventKey { Pressed = false, PhysicalKeycode = Key.Right });
        Expect.Equal(0, context.Actions.Count, "Echo and release must not complete a pending diagonal.");
        Press(context.Root, Key.Right);
        context.Root._UnhandledInput(new InputEventKey { Pressed = true, Echo = true, PhysicalKeycode = Key.Right });
        Expect.Equal(1, context.Actions.Count, "Echo after completion must not add another move.");
        Expect.Equal(new Position(4, 2), context.World.Player.Position, "The real press should complete exactly one diagonal.");
    }

    private static void PlainArrowsRemainCardinal()
    {
        var context = CreateContext();
        context.World.AddEntity(new StubEntity("Diagonal enemy", new Position(4, 2)));
        Press(context.Root, Key.Right);
        Expect.Equal(new Position(4, 3), context.World.Player.Position, "A plain arrow must not auto-attack a diagonal neighbor.");
        Expect.True(context.Actions[0] is MoveAction, "Plain arrows retain the exact destination action.");
        Press(context.Root, Key.Up);
        Expect.True(context.Actions[1] is AttackAction, "An exact-destination hostile should still be attacked.");
    }

    private static void MovesPreserveCorners()
    {
        var context = CreateContext();
        context.World.SetTile(new Position(3, 2), TileType.Wall);
        context.World.SetTile(new Position(4, 3), TileType.Wall);
        Press(context.Root, Key.V);
        Press(context.Root, Key.Up);
        Press(context.Root, Key.Right);
        Expect.Equal(new Position(3, 3), context.World.Player.Position, "Both blocked side tiles must prevent diagonal movement.");
        Expect.Equal(0, context.World.TurnNumber, "A blocked move must not consume a turn.");
        context.World.SetTile(new Position(4, 3), TileType.Floor);
        Press(context.Root, Key.Pageup);
        Expect.Equal(new Position(4, 2), context.World.Player.Position, "One open side should allow the existing diagonal movement rule.");
        Expect.Equal(1, context.World.TurnNumber, "Only the successful move should spend a turn.");
    }

    private static void RunRemainsCardinal()
    {
        var context = CreateContext();
        Press(context.Root, Key.R);
        foreach (var key in new[] { Key.Home, Key.Pageup, Key.End, Key.Pagedown, Key.Kp7, Key.Kp9, Key.Kp1, Key.Kp3 })
        {
            Press(context.Root, key);
        }

        Expect.Equal(0, context.Actions.Count, "Diagonal shortcuts must not become autoplay directions.");
        Expect.Equal(0, context.World.TurnNumber, "Unsupported run directions must not spend a turn.");
        Expect.True(context.Root.InputHandler.IsRunPrefixActive, "Unsupported keys should preserve existing run-prefix behavior.");
        Press(context.Root, Key.Escape);
        Press(context.Root, Key.Right);
        Expect.Equal(new Position(4, 3), context.World.Player.Position, "Cancelling run should restore ordinary movement.");
    }

    private static void RangedDirectionSubmitsRangedAction()
    {
        var context = CreateContext();
        var content = (StubContentDatabase)context.Manager.Content!;
        var items = (Dictionary<string, ItemTemplate>)content.ItemTemplates;
        items[RangedAttackAction.ArrowTemplateId] = new(RangedAttackAction.ArrowTemplateId, "Arrows", "", ItemCategory.Consumable,
            EquipSlot.None, new Dictionary<string, int>(), null, 0, 20, "common");
        var bow = new ItemTemplate("bow_test", "Bow", "", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", Tags: new[] { "ranged" });
        items[bow.TemplateId] = bow;
        var inventory = context.World.Player.GetComponent<InventoryComponent>()!;
        inventory.Add(new ItemInstance { TemplateId = RangedAttackAction.ArrowTemplateId, StackCount = 3 });
        var bowInstance = new ItemInstance { TemplateId = bow.TemplateId };
        inventory.Add(bowInstance);
        Expect.True(inventory.TryEquip(bowInstance, EquipSlot.MainHand, bow.StatModifiers, out _), "The test bow should equip.");
        context.World.SetTile(new Position(6, 3), TileType.Floor);
        var enemy = new StubEntity("Target", new Position(6, 3), Faction.Enemy);
        context.World.AddEntity(enemy);
        context.World.SetVisible(enemy.Position, true);

        Press(context.Root, Key.Right);

        Expect.True(context.Actions.Count == 1 && context.Actions[0] is RangedAttackAction ranged && ranged.TargetId == enemy.Id,
            "A directional input with a distant hostile should submit the existing ranged action.");
        Expect.Equal(new Position(3, 3), context.World.Player.Position, "Ranged input must not reposition the player.");
    }

    private static void Press(UIRoot root, Key key) =>
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = key });

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(7, 7);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), x == 0 || y == 0 || x == 6 || y == 6 ? TileType.Wall : TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(3, 3), Faction.Player,
            stats: new Stats { HP = 40, MaxHP = 40, Attack = 8, Accuracy = 100, Speed = 100, ViewRadius = 8 });
        player.SetComponent(new InventoryComponent(20));
        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private static Context CreateContext()
    {
        var world = CreateWorld();
        var bus = new EventBus();
        var content = new StubContentDatabase();
        var manager = new GameManager();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        manager.LoadWorld(world);
        var root = new UIRoot();
        root.BindServices(manager, bus, content);
        var actions = new List<IAction>();
        bus.PlayerActionSubmitted += actions.Add;
        return new Context(world, manager, bus, root, actions);
    }

    private sealed record Context(WorldState World, GameManager Manager, EventBus Bus, UIRoot Root, List<IAction> Actions);
}
