using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public enum FloorType : byte
{
    StandardFloor,
    SafeFloor,
    BossFloor,
}

public enum SpecialRoomType : byte
{
    ShrineRoom,
    CurseRoom,
    BossRoom,
}

public sealed record SpecialRoomRequest(SpecialRoomType Type, string EventId, string PrefabTag);

public sealed record FloorEventPlan(FloorType FloorType, IReadOnlyList<SpecialRoomRequest> SpecialRooms)
{
    public bool HasSpecialRoom(SpecialRoomType type)
    {
        foreach (var room in SpecialRooms)
        {
            if (room.Type == type)
            {
                return true;
            }
        }

        return false;
    }
}

public static class FloorEventResolver
{
    public static FloorType ResolveFloorType(int depth, int seed)
    {
        if (depth > 0 && depth % 5 == 0)
        {
            return FloorType.SafeFloor;
        }

        if (depth > 0 && depth % 3 == 0)
        {
            return FloorType.BossFloor;
        }

        return FloorType.StandardFloor;
    }

    public static FloorEventPlan ResolveFloorEvents(int depth, int seed)
    {
        var floorType = ResolveFloorType(depth, seed);
        var rooms = new List<SpecialRoomRequest>();

        if (floorType == FloorType.BossFloor)
        {
            rooms.Add(new SpecialRoomRequest(SpecialRoomType.BossRoom, "boss_room", "boss"));
            return new FloorEventPlan(floorType, rooms);
        }

        if (floorType != FloorType.StandardFloor)
        {
            return new FloorEventPlan(floorType, rooms);
        }

        var rng = new Random(MixSeed(depth, seed));
        if (rng.NextDouble() < 0.40)
        {
            rooms.Add(new SpecialRoomRequest(SpecialRoomType.ShrineRoom, ResolveShrineEventId(rng), "shrine"));
        }

        if (rng.NextDouble() < 0.25)
        {
            rooms.Add(new SpecialRoomRequest(SpecialRoomType.CurseRoom, "curse_room", "curse"));
        }

        return new FloorEventPlan(floorType, rooms);
    }

    public static bool ShouldSkipEnemySpawns(int depth, int seed) => ResolveFloorType(depth, seed) == FloorType.SafeFloor;

    private static string ResolveShrineEventId(Random rng)
    {
        return rng.Next(3) switch
        {
            0 => "shrine_perk",
            1 => "shrine_stat",
            _ => "shrine_relic",
        };
    }

    private static int MixSeed(int depth, int seed)
    {
        unchecked
        {
            return seed ^ (depth * 73856093) ^ 0x4f1bbcdc;
        }
    }
}
