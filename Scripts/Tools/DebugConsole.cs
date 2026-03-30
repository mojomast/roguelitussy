using System;
using System.Collections.Generic;
using Godot;

namespace Roguelike.Godot;

public partial class DebugConsole : CanvasLayer
{
    private Panel _panel = null!;
    private RichTextLabel _output = null!;
    private LineEdit _input = null!;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _godMode;

    private readonly Dictionary<string, Action<string[]>> _commands = new();

    public override void _Ready()
    {
        Layer = 100;
        _panel = GetNode<Panel>("Panel");
        _output = _panel.GetNode<RichTextLabel>("VBox/Output");
        _input = _panel.GetNode<LineEdit>("VBox/Input");

        _panel.Visible = false;

        _input.TextSubmitted += OnCommandSubmitted;

        RegisterCommands();
        Print("[color=gray]Debug console ready. Type 'help' for commands.[/color]");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Quoteleft })
        {
            ToggleConsole();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_panel.Visible) return;

        if (@event is InputEventKey { Pressed: true } key)
        {
            switch (key.Keycode)
            {
                case Key.Up:
                    NavigateHistory(-1);
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Down:
                    NavigateHistory(1);
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }
    }

    private void ToggleConsole()
    {
        _panel.Visible = !_panel.Visible;
        if (_panel.Visible)
        {
            _input.GrabFocus();
            _input.Clear();
        }
    }

    private void OnCommandSubmitted(string text)
    {
        string trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        _history.Add(trimmed);
        _historyIndex = _history.Count;

        Print($"[color=white]> {trimmed}[/color]");

        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLowerInvariant();
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (_commands.TryGetValue(cmd, out var handler))
        {
            try
            {
                handler(args);
            }
            catch (Exception ex)
            {
                Print($"[color=red]Error: {ex.Message}[/color]");
            }
        }
        else
        {
            Print($"[color=red]Unknown command: {cmd}[/color]");
        }

        _input.Clear();
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count - 1);
        _input.Text = _history[_historyIndex];
        _input.CaretColumn = _input.Text.Length;
    }

    private void Print(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _output.AppendText($"[color=gray][{timestamp}][/color] {message}\n");
    }

    // ═══════════════════════════════════════════
    //  COMMAND REGISTRATION
    // ═══════════════════════════════════════════

    private void RegisterCommands()
    {
        _commands["help"] = CmdHelp;
        _commands["spawn"] = CmdSpawn;
        _commands["tp"] = CmdTeleport;
        _commands["heal"] = CmdHeal;
        _commands["kill"] = CmdKill;
        _commands["god"] = CmdGod;
        _commands["reveal"] = CmdReveal;
        _commands["depth"] = CmdDepth;
        _commands["stats"] = CmdStats;
        _commands["fps"] = CmdFps;
    }

    // ═══════════════════════════════════════════
    //  COMMANDS
    // ═══════════════════════════════════════════

    private void CmdHelp(string[] _)
    {
        Print("[color=yellow]Available commands:[/color]");
        Print("  help              - Show this list");
        Print("  spawn <enemy_id>  - Spawn enemy near player");
        Print("  tp <x> <y>        - Teleport player");
        Print("  heal [amount]     - Heal player (default: full)");
        Print("  kill              - Kill entity at player pos");
        Print("  god               - Toggle god mode");
        Print("  reveal            - Reveal entire map");
        Print("  depth <n>         - Set dungeon depth");
        Print("  stats             - Show player stats");
        Print("  fps               - Toggle FPS overlay");
    }

    private void CmdSpawn(string[] args)
    {
        if (args.Length < 1)
        {
            Print("[color=red]Usage: spawn <enemy_id>[/color]");
            return;
        }

        string enemyId = args[0];
        // TODO: Wire to ContentLoader + WorldState to spawn enemy
        // var template = ContentLoader.GetEnemy(enemyId);
        // var pos = world.Player.Position.Offset(1, 0);
        Print($"[color=yellow]spawn '{enemyId}' - not yet wired to simulation.[/color]");
    }

    private void CmdTeleport(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
        {
            Print("[color=red]Usage: tp <x> <y>[/color]");
            return;
        }

        // TODO: Wire to WorldState
        // world.UpdateEntityPosition(world.Player.Id, world.Player.Position, new Position(x, y));
        // world.Player.Position = new Position(x, y);
        Print($"[color=green]Teleported to ({x}, {y}) - not yet wired.[/color]");
    }

    private void CmdHeal(string[] args)
    {
        int amount = -1; // -1 means full heal
        if (args.Length >= 1 && int.TryParse(args[0], out int parsed))
            amount = parsed;

        // TODO: Wire to Player entity
        // var stats = world.Player.Stats;
        // int healAmt = amount < 0 ? stats.MaxHP - stats.HP : amount;
        // stats.HP = Math.Min(stats.HP + healAmt, stats.MaxHP);
        string desc = amount < 0 ? "full" : amount.ToString();
        Print($"[color=green]Healed ({desc}) - not yet wired.[/color]");
    }

    private void CmdKill(string[] args)
    {
        // TODO: Wire to WorldState - kill entity at player position or under cursor
        // var target = world.GetEntityAt(world.Player.Position.Offset(0,0));
        Print("[color=yellow]kill - not yet wired to simulation.[/color]");
    }

    private void CmdGod(string[] _)
    {
        _godMode = !_godMode;
        // TODO: Wire to player invincibility flag
        string state = _godMode ? "ON" : "OFF";
        Print($"[color=cyan]God mode: {state}[/color]");
    }

    private void CmdReveal(string[] _)
    {
        // TODO: Wire to WorldState - set all tiles explored + visible
        // for (int y = 0; y < world.Height; y++)
        //     for (int x = 0; x < world.Width; x++)
        //         world.SetVisible(new Position(x, y), true);
        Print("[color=green]Map revealed - not yet wired.[/color]");
    }

    private void CmdDepth(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int depth))
        {
            Print("[color=red]Usage: depth <n>[/color]");
            return;
        }

        // TODO: Wire to WorldState
        // world.Depth = depth;
        Print($"[color=green]Depth set to {depth} - not yet wired.[/color]");
    }

    private void CmdStats(string[] _)
    {
        // TODO: Wire to player entity
        // var s = world.Player.Stats;
        // Print($"HP: {s.HP}/{s.MaxHP}  ATK: {s.Attack}  DEF: {s.Defense}  SPD: {s.Speed}");
        Print("[color=yellow]stats - not yet wired to simulation.[/color]");
    }

    private void CmdFps(string[] _)
    {
        // Toggle the DebugOverlay if it exists in the scene tree
        var overlay = GetTree().Root.FindChild("DebugOverlay", true, false) as DebugOverlay;
        if (overlay is not null)
        {
            overlay.Toggle();
            Print("[color=green]FPS overlay toggled.[/color]");
        }
        else
        {
            Print("[color=red]DebugOverlay not found in scene tree.[/color]");
        }
    }
}
