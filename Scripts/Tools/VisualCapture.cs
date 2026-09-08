using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Roguelike.Core;

namespace Godotussy;

// Explicit test scene only: use a real renderer, never the headless dummy driver.
public partial class VisualCapture : Node
{
    private bool _assertLayout;

    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            _assertLayout = args.Contains("--assert-layout");
            var output = args.FirstOrDefault(arg => arg.StartsWith("--capture-output=", StringComparison.Ordinal))?[17..];
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new ArgumentException("Specify --capture-output=<directory> after --.");
            }

            Directory.CreateDirectory(output);
            var sizeArg = args.FirstOrDefault(arg => arg.StartsWith("--capture-size=", StringComparison.Ordinal))?[15..] ?? "1280x720";
            var dimensions = sizeArg.Split('x');
            GetTree().Root.ContentScaleSize = new Vector2I(int.Parse(dimensions[0]), int.Parse(dimensions[1]));
            var main = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate();
            AddChild(main);
            var root = main.GetNode<UIRoot>("UiRoot");
            var manager = GetNode<GameManager>("/root/GameManager");
            await Capture(root, output, "title");
            for (var i = 0; i < 18; i++)
            {
                root.MainMenu.HandleKey(Key.Down);
            }

            await Capture(root, output, "title-late-selection");
            manager.StartNewGame(1337);
            root.MainMenu.Close();
            root.BindServices(manager, GetNode<EventBus>("/root/EventBus"), manager.Content);
            manager.SetMapReveal(true);
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.B });
            await Capture(root, output, "abilities");
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });

            var activeWorld = manager.World ?? throw new InvalidOperationException("New run did not create a world.");
            var itemPosition = FindVisibleFloorNearPlayer(activeWorld);
            activeWorld.DropItem(itemPosition, new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true });
            activeWorld.DropItem(itemPosition, new ItemInstance { TemplateId = "scroll_fireball", IsIdentified = true });
            GetNode<EventBus>("/root/EventBus").EmitFovRecalculated();
            await Capture(root, output, "ground-items");
            root.Inventory.Open();
            await Capture(root, output, "inventory");
            root.Inventory.Close();

            var npc = manager.World!.Entities.First(entity => entity.GetComponent<NpcComponent>()?.TemplateId == "quartermaster_vale");
            OpenDialog(root, manager, npc);
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Down });
            await Capture(root, output, "dialog");
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
            var merchant = manager.World.Entities.First(entity => entity.GetComponent<MerchantComponent>() is not null);
            root.ShopUI.Open(merchant.Id);
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Down });
            await Capture(root, output, "shop");
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });

            var sen = manager.World.Entities.First(entity => entity.GetComponent<NpcComponent>()?.TemplateId == "field_chronicler");
            OpenDialog(root, manager, sen);
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Key1 });
            await Capture(root, output, "sen-report");
            root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });

            foreach (var depth in new[] { 1, 4, 7 })
            {
                manager.TravelToFloor(depth);
                manager.SetMapReveal(true);
                if (root.FloorSummaryUI.Visible)
                {
                    root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });
                }
                root.FloorEventPopupUI.Visible = false;
                root.RelicChoiceOverlay.Close();
                await Capture(root, output, $"floor-{depth}");
                var visitor = manager.World!.Entities.FirstOrDefault(entity => entity.GetComponent<NpcComponent>()?.TemplateId == (depth == 4 ? "sister_ilex" : "cinder_broker_orin"));
                if (visitor is not null)
                {
                    // Detached visual fixture only: expose the injured treatment branch.
                    manager.World.Player.Stats.HP = manager.World.Player.Stats.MaxHP - 10;
                    OpenDialog(root, manager, visitor);
                    root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Down });
                    await Capture(root, output, $"npc-{depth}");
                    root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
                }
            }

            GD.Print($"Visual captures saved to {output}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static void OpenDialog(UIRoot root, GameManager manager, IEntity npc)
    {
        foreach (var delta in new[] { new Position(0, -1), new Position(1, 0), new Position(0, 1), new Position(-1, 0) })
        {
            var position = npc.Position + delta;
            if (manager.World!.IsWalkable(position) && manager.TeleportPlayer(position)
                && manager.GetInteractionContext()?.NpcId == npc.Id)
            {
                root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.F });
                if (root.DialogUI.Visible)
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException($"Could not open normal F interaction with {npc.Name}.");
    }

    private static Position FindVisibleFloorNearPlayer(WorldState world)
    {
        foreach (var delta in new[] { new Position(1, 0), new Position(-1, 0), new Position(0, 1), new Position(0, -1) })
        {
            var position = world.Player.Position + delta;
            if (world.InBounds(position) && world.IsWalkable(position) && world.IsVisible(position))
            {
                return position;
            }
        }

        throw new InvalidOperationException("Capture fixture could not find a visible floor beside the player.");
    }

    private async Task Capture(Node root, string output, string name)
    {
        // Allow container minimum sizes and deferred layout to settle before readback.
        for (var i = 0; i < 8; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(Path.Combine(output, name + ".png"));
        if (error != Error.Ok)
        {
            throw new IOException($"Screenshot {name} failed: {error}");
        }

        var labels = new List<object>();
        var errors = new List<string>();
        CollectLabels(root, labels, errors);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(labels, new JsonSerializerOptions { WriteIndented = true }));
        if (_assertLayout && errors.Count > 0)
        {
            throw new InvalidOperationException("Labels extend outside their parent: " + string.Join(", ", errors));
        }
    }

    private static void CollectLabels(Node node, List<object> labels, List<string> errors)
    {
        if (node is Control control && control.IsVisibleInTree() && node is Label or RichTextLabel)
        {
            var rect = control.GetGlobalRect();
            var parent = control.GetParent() as Control;
            var fits = parent is null || parent.GetGlobalRect().Grow(0.5f).Encloses(rect);
            if (!fits)
            {
                errors.Add(node.GetPath().ToString());
            }
            labels.Add(new
            {
                Path = node.GetPath().ToString(),
                Text = node is Label label ? label.Text : ((RichTextLabel)node).GetParsedText(),
                X = rect.Position.X,
                Y = rect.Position.Y,
                Width = rect.Size.X,
                Height = rect.Size.Y,
                FitsParent = fits,
                MinimumWidth = control.GetCombinedMinimumSize().X,
                MinimumHeight = control.GetCombinedMinimumSize().Y,
            });
        }

        foreach (var child in node.GetChildren())
        {
            CollectLabels(child, labels, errors);
        }
    }
}
