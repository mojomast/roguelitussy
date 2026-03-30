using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roguelike.Core.Persistence;

public static class WorldStateSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string Serialize(WorldState world)
    {
        var root = new JsonObject
        {
            ["version"] = SaveMigrator.CurrentVersion,
            ["seed"] = world.Seed,
            ["turnNumber"] = world.TurnNumber,
            ["depth"] = world.Depth,
            ["width"] = world.Width,
            ["height"] = world.Height,
            ["grid"] = SerializeGrid(world.GetRawGrid()),
            ["explored"] = SerializeExplored(world.GetRawExplored()),
            ["playerEntityId"] = world.Player.Id.Value.ToString(),
            ["metadata"] = new JsonObject
            {
                ["playerName"] = world.Player.Name,
                ["savedAt"] = DateTime.UtcNow.ToString("o"),
            },
        };

        var entities = new JsonArray();
        foreach (var entity in world.Entities)
            entities.Add(EntitySerializer.Serialize(entity));
        root["entities"] = entities;

        return root.ToJsonString(WriteOptions);
    }

    public static WorldState Deserialize(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();

        int version = root["version"]!.GetValue<int>();
        if (version != SaveMigrator.CurrentVersion)
            root = SaveMigrator.Migrate(root, version, SaveMigrator.CurrentVersion);

        int width = root["width"]!.GetValue<int>();
        int height = root["height"]!.GetValue<int>();

        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = root["seed"]!.GetValue<int>();
        world.TurnNumber = root["turnNumber"]!.GetValue<int>();
        world.Depth = root["depth"]!.GetValue<int>();

        DeserializeGrid(root["grid"]!.GetValue<string>(), world, width, height);
        DeserializeExplored(root["explored"]!.GetValue<string>(), world, width, height);

        var playerIdStr = root["playerEntityId"]!.GetValue<string>();
        var playerId = new EntityId(Guid.Parse(playerIdStr));

        foreach (var entityNode in root["entities"]!.AsArray())
        {
            var entity = EntitySerializer.Deserialize(entityNode!.AsObject());
            world.AddEntity(entity);

            if (entity.Id.Equals(playerId))
                world.Player = entity;
        }

        return world;
    }

    public static SaveMetadata? DeserializeMetadata(string json, int slotIndex)
    {
        var root = JsonNode.Parse(json)?.AsObject();
        if (root is null)
            return null;

        var meta = root["metadata"]?.AsObject();
        if (meta is null)
            return null;

        return new SaveMetadata(
            slotIndex,
            root["depth"]?.GetValue<int>() ?? 0,
            root["turnNumber"]?.GetValue<int>() ?? 0,
            meta["playerName"]?.GetValue<string>() ?? "Unknown",
            DateTime.TryParse(meta["savedAt"]?.GetValue<string>(), out var dt) ? dt : DateTime.MinValue,
            root["version"]?.GetValue<int>() ?? 1
        );
    }

    private static string SerializeGrid(TileType[] grid)
    {
        var bytes = new byte[grid.Length];
        for (int i = 0; i < grid.Length; i++)
            bytes[i] = (byte)grid[i];
        return Convert.ToBase64String(bytes);
    }

    private static void DeserializeGrid(string base64, WorldState world, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                world.SetTile(new Position(x, y), (TileType)bytes[y * width + x]);
    }

    private static string SerializeExplored(bool[] explored)
    {
        // Pack bools into bytes (8 per byte)
        int byteCount = (explored.Length + 7) / 8;
        var bytes = new byte[byteCount];
        for (int i = 0; i < explored.Length; i++)
        {
            if (explored[i])
                bytes[i / 8] |= (byte)(1 << (i % 8));
        }
        return Convert.ToBase64String(bytes);
    }

    private static void DeserializeExplored(string base64, WorldState world, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                bool explored = (bytes[i / 8] & (1 << (i % 8))) != 0;
                if (explored)
                {
                    world.SetVisible(new Position(x, y), true);
                    world.SetVisible(new Position(x, y), false);
                }
            }
    }
}
