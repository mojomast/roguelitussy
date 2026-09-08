using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class SaveRunSnapshot
{
    public SaveRunSnapshot(int seed, int currentFloor, WorldState activeWorld, IReadOnlyDictionary<int, WorldState> floors, CharacterOptionsSaveData? characterOptions = null, IReadOnlyCollection<int>? rewardedFloorDepths = null)
    {
        ArgumentNullException.ThrowIfNull(activeWorld);
        ArgumentNullException.ThrowIfNull(floors);

        Seed = seed;
        CurrentFloor = currentFloor;
        ActiveWorld = activeWorld;
        Floors = new Dictionary<int, WorldState>(floors);
        CharacterOptions = characterOptions ?? new CharacterOptionsSaveData();
        RewardedFloorDepths = new List<int>(rewardedFloorDepths ?? Array.Empty<int>());
    }

    public int Seed { get; }

    public int CurrentFloor { get; }

    public WorldState ActiveWorld { get; }

    public IReadOnlyDictionary<int, WorldState> Floors { get; }

    public CharacterOptionsSaveData CharacterOptions { get; }

    public IReadOnlyList<int> RewardedFloorDepths { get; }
}
