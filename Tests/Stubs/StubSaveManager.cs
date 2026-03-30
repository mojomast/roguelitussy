using System;
using System.Threading.Tasks;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubSaveManager : ISaveManager
{
    private readonly WorldState?[] _slots = new WorldState?[3];

    public Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return Task.FromResult(false);
        _slots[slotIndex] = world;
        return Task.FromResult(true);
    }

    public Task<WorldState?> LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return Task.FromResult<WorldState?>(null);
        return Task.FromResult(_slots[slotIndex]);
    }

    public bool HasSave(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _slots.Length && _slots[slotIndex] != null;

    public void DeleteSave(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Length)
            _slots[slotIndex] = null;
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex) =>
        _slots[slotIndex] is { } w
            ? new SaveMetadata(slotIndex, w.Depth, w.TurnNumber, "Test Player", DateTime.UtcNow, 1)
            : null;
}
