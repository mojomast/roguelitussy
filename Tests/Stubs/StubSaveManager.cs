using System.Collections.Generic;
using System.Threading.Tasks;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubSaveManager : ISaveManager
{
    private const int MetadataVersion = 8;
    private readonly Dictionary<int, SaveRunSnapshot> _slots = new();
    private readonly Dictionary<int, SaveMetadata> _metadata = new();

    public Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        return SaveRun(new SaveRunSnapshot(world.Seed, world.Depth, world, new Dictionary<int, WorldState> { [world.Depth] = world }), slotIndex);
    }

    public Task<bool> SaveRun(SaveRunSnapshot snapshot, int slotIndex)
    {
        _slots[slotIndex] = snapshot;
        _metadata[slotIndex] = new SaveMetadata(slotIndex, snapshot.CurrentFloor, snapshot.ActiveWorld.TurnNumber, snapshot.ActiveWorld.Player.Name, System.DateTime.UtcNow, MetadataVersion);
        return Task.FromResult(true);
    }

    public Task<WorldState?> LoadGame(int slotIndex)
    {
        return _slots.TryGetValue(slotIndex, out var snapshot)
            ? Task.FromResult<WorldState?>(snapshot.ActiveWorld)
            : Task.FromResult<WorldState?>(null);
    }

    public Task<SaveRunSnapshot?> LoadRun(int slotIndex)
    {
        return _slots.TryGetValue(slotIndex, out var snapshot)
            ? Task.FromResult<SaveRunSnapshot?>(snapshot)
            : Task.FromResult<SaveRunSnapshot?>(null);
    }

    public bool HasSave(int slotIndex) => _slots.ContainsKey(slotIndex);

    public void DeleteSave(int slotIndex)
    {
        _slots.Remove(slotIndex);
        _metadata.Remove(slotIndex);
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex) => _metadata.TryGetValue(slotIndex, out var metadata) ? metadata : null;
}
