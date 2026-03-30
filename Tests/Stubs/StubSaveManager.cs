using System.Collections.Generic;
using System.Threading.Tasks;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubSaveManager : ISaveManager
{
    private readonly Dictionary<int, WorldState> _slots = new();
    private readonly Dictionary<int, SaveMetadata> _metadata = new();

    public Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        _slots[slotIndex] = world;
        _metadata[slotIndex] = new SaveMetadata(slotIndex, world.Depth, world.TurnNumber, world.Player.Name, System.DateTime.UtcNow, 1);
        return Task.FromResult(true);
    }

    public Task<WorldState?> LoadGame(int slotIndex)
    {
        _slots.TryGetValue(slotIndex, out var world);
        return Task.FromResult(world);
    }

    public bool HasSave(int slotIndex) => _slots.ContainsKey(slotIndex);

    public void DeleteSave(int slotIndex)
    {
        _slots.Remove(slotIndex);
        _metadata.Remove(slotIndex);
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex) => _metadata.TryGetValue(slotIndex, out var metadata) ? metadata : null;
}
