using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;

namespace Godotussy;

public sealed class DebugCommandProcessor
{
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
    }

    public IReadOnlyList<string> GetCommands()
    {
        return new[]
        {
            "help",
            "where",
            "heal [amount]",
            "reveal",
            "fog",
            "teleport <x> <y>",
            "spawn_item <templateId> [count]",
            "wait",
            "floor <depth>",
            "door <open|close> <x> <y>",
            "save <slot>",
            "load <slot>",
            "list <items|enemies>",
        };
    }

    public string Execute(string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            return "No command provided.";
        }

        var tokens = rawCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return "No command provided.";
        }

        return tokens[0].ToLowerInvariant() switch
        {
            "help" => BuildHelpText(),
            "where" => ExecuteWhere(),
            "heal" => ExecuteHeal(tokens),
            "reveal" => ExecuteReveal(true),
            "fog" => ExecuteReveal(false),
            "teleport" => ExecuteTeleport(tokens),
            "spawn_item" => ExecuteSpawnItem(tokens),
            "wait" => ExecuteWait(),
            "floor" => ExecuteFloor(tokens),
            "door" => ExecuteDoor(tokens),
            "save" => ExecuteSave(tokens),
            "load" => ExecuteLoad(tokens),
            "list" => ExecuteList(tokens),
            _ => $"Unknown command '{tokens[0]}'. Type 'help' for the command list.",
        };
    }

    private string BuildHelpText()
    {
        return "Commands: " + string.Join(", ", GetCommands());
    }

    private string ExecuteWhere()
    {
        if (!TryGetWorld(out var world, out var player, out var error))
        {
            return error;
        }

        return $"Player at {player.Position.X},{player.Position.Y}. Floor {world.Depth}. HP {player.Stats.HP}/{player.Stats.MaxHP}.";
    }

    private string ExecuteHeal(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out _, out var player, out var error))
        {
            return error;
        }

        var amount = player.Stats.MaxHP;
        if (tokens.Count > 1 && (!int.TryParse(tokens[1], out amount) || amount <= 0))
        {
            return "Usage: heal [amount]";
        }

        player.Stats.HP = Math.Min(player.Stats.MaxHP, player.Stats.HP + amount);
        _eventBus?.EmitHPChanged(player.Id, player.Stats.HP, player.Stats.MaxHP);
        return $"Healed player to {player.Stats.HP}/{player.Stats.MaxHP}.";
    }

    private string ExecuteReveal(bool reveal)
    {
        if (!TryGetWorld(out var world, out _, out var error))
        {
            return error;
        }

        if (reveal)
        {
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetVisible(new Position(x, y), true);
                }
            }
        }
        else
        {
            world.ClearVisibility();
        }

        _eventBus?.EmitFovRecalculated();
        return reveal ? "Entire map revealed." : "Visibility cleared.";
    }

    private string ExecuteTeleport(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out var world, out var player, out var error))
        {
            return error;
        }

        if (tokens.Count < 3 || !int.TryParse(tokens[1], out var x) || !int.TryParse(tokens[2], out var y))
        {
            return "Usage: teleport <x> <y>";
        }

        var target = new Position(x, y);
        if (!world.InBounds(target))
        {
            return $"Target {x},{y} is out of bounds.";
        }

        if (!world.IsWalkable(target))
        {
            return $"Target {x},{y} is not walkable.";
        }

        var origin = player.Position;
        if (!world.MoveEntity(player.Id, target))
        {
            return $"Could not move player to {x},{y}.";
        }

        _eventBus?.EmitEntityMoved(player.Id, origin, target);
        _eventBus?.EmitFovRecalculated();
        return $"Teleported player to {x},{y}.";
    }

    private string ExecuteSpawnItem(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out _, out var player, out var error))
        {
            return error;
        }

        if (tokens.Count < 2)
        {
            return "Usage: spawn_item <templateId> [count]";
        }

        if (_content is null || !_content.TryGetItemTemplate(tokens[1], out var template))
        {
            return $"Item template '{tokens[1]}' was not found.";
        }

        var count = 1;
        if (tokens.Count > 2 && (!int.TryParse(tokens[2], out count) || count <= 0))
        {
            return "Usage: spawn_item <templateId> [count]";
        }

        var inventory = player.GetComponent<InventoryComponent>() ?? new InventoryComponent();
        player.SetComponent(inventory);

        var added = 0;
        for (var index = 0; index < count; index++)
        {
            var item = new ItemInstance
            {
                TemplateId = template.TemplateId,
                CurrentCharges = template.MaxCharges > 0 ? template.MaxCharges : 0,
                StackCount = template.MaxStack > 1 ? 1 : 1,
            };

            if (!inventory.Add(item))
            {
                break;
            }

            added++;
        }

        _eventBus?.EmitInventoryChanged(player.Id);
        return added == 0
            ? "Inventory is full. No items were added."
            : $"Added {added}x {template.DisplayName} to the player inventory.";
    }

    private string ExecuteWait()
    {
        if (!TryGetWorld(out var world, out var player, out var error))
        {
            return error;
        }

        if (_gameManager is null)
        {
            return "Game manager is not available.";
        }

        var outcome = _gameManager.ProcessPlayerAction(new WaitAction(player.Id));
        return outcome.Result == ActionResult.Success
            ? "Player waited one turn."
            : $"Wait failed: {outcome.Result}.";
    }

    private string ExecuteFloor(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out var world, out _, out var error))
        {
            return error;
        }

        if (tokens.Count < 2 || !int.TryParse(tokens[1], out var depth) || depth < 0)
        {
            return "Usage: floor <depth>";
        }

        world.Depth = depth;
        _gameManager?.TransitionFloor(depth);
        return $"Floor set to {depth}.";
    }

    private string ExecuteDoor(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out var world, out _, out var error))
        {
            return error;
        }

        if (tokens.Count < 4 || !int.TryParse(tokens[2], out var x) || !int.TryParse(tokens[3], out var y))
        {
            return "Usage: door <open|close> <x> <y>";
        }

        var position = new Position(x, y);
        if (!world.InBounds(position) || world.GetTile(position) != TileType.Door)
        {
            return $"Tile {x},{y} is not a door.";
        }

        var open = tokens[1].Equals("open", StringComparison.OrdinalIgnoreCase);
        var close = tokens[1].Equals("close", StringComparison.OrdinalIgnoreCase);
        if (!open && !close)
        {
            return "Usage: door <open|close> <x> <y>";
        }

        world.SetDoorOpen(position, open);
        _eventBus?.EmitFovRecalculated();
        return open ? $"Opened door at {x},{y}." : $"Closed door at {x},{y}.";
    }

    private string ExecuteSave(IReadOnlyList<string> tokens)
    {
        if (!TryGetWorld(out var world, out _, out var error))
        {
            return error;
        }

        if (_gameManager?.SaveManager is null)
        {
            return "Save manager is not available.";
        }

        if (tokens.Count < 2 || !int.TryParse(tokens[1], out var slot) || slot < 0)
        {
            return "Usage: save <slot>";
        }

        var success = _gameManager.SaveManager.SaveGame(world, slot).GetAwaiter().GetResult();
        _eventBus?.EmitSaveCompleted(success);
        return success ? $"Saved slot {slot}." : $"Save failed for slot {slot}.";
    }

    private string ExecuteLoad(IReadOnlyList<string> tokens)
    {
        if (_gameManager?.SaveManager is null)
        {
            return "Save manager is not available.";
        }

        if (tokens.Count < 2 || !int.TryParse(tokens[1], out var slot) || slot < 0)
        {
            return "Usage: load <slot>";
        }

        var world = _gameManager.SaveManager.LoadGame(slot).GetAwaiter().GetResult();
        if (world is null)
        {
            _eventBus?.EmitLoadCompleted(false);
            return $"Load failed for slot {slot}.";
        }

        _gameManager.LoadWorld(world);
        _eventBus?.EmitFloorChanged(world.Depth);
        _eventBus?.EmitLoadCompleted(true);
        _eventBus?.EmitTurnCompleted();
        return $"Loaded slot {slot}.";
    }

    private string ExecuteList(IReadOnlyList<string> tokens)
    {
        if (_content is null || tokens.Count < 2)
        {
            return "Usage: list <items|enemies>";
        }

        return tokens[1].ToLowerInvariant() switch
        {
            "items" => "Items: " + string.Join(", ", _content.ItemTemplates.Keys.OrderBy(id => id, StringComparer.Ordinal)),
            "enemies" => "Enemies: " + string.Join(", ", _content.EnemyTemplates.Keys.OrderBy(id => id, StringComparer.Ordinal)),
            _ => "Usage: list <items|enemies>",
        };
    }

    private bool TryGetWorld(out WorldState world, out IEntity player, out string error)
    {
        world = _gameManager?.World!;
        player = world?.Player!;
        if (world is null || player is null)
        {
            error = "World is not loaded.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}