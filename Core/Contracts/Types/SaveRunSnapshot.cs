using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class SaveRunSnapshot
{
    public SaveRunSnapshot(int seed, int currentFloor, WorldState activeWorld, IReadOnlyDictionary<int, WorldState> floors)
    {
        ArgumentNullException.ThrowIfNull(activeWorld);
        ArgumentNullException.ThrowIfNull(floors);

        Seed = seed;
        CurrentFloor = currentFloor;
        ActiveWorld = activeWorld;
        Floors = new Dictionary<int, WorldState>(floors);
    }

    public int Seed { get; }

    public int CurrentFloor { get; }

    public WorldState ActiveWorld { get; }

    public IReadOnlyDictionary<int, WorldState> Floors { get; }
}
