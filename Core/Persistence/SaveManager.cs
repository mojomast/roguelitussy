using System;
using System.IO;
using System.Threading.Tasks;

namespace Roguelike.Core.Persistence;

public sealed class SaveManager : ISaveManager
{
    private readonly string _basePath;

    public SaveManager(string basePath)
    {
        _basePath = basePath;
    }

    private string GetSavePath(int slotIndex) =>
        Path.Combine(_basePath, $"save_{slotIndex}.json");

    public Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        try
        {
            Directory.CreateDirectory(_basePath);
            var json = WorldStateSerializer.Serialize(world);
            File.WriteAllText(GetSavePath(slotIndex), json);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<WorldState?> LoadGame(int slotIndex)
    {
        try
        {
            var path = GetSavePath(slotIndex);
            if (!File.Exists(path))
                return Task.FromResult<WorldState?>(null);

            var json = File.ReadAllText(path);
            var world = WorldStateSerializer.Deserialize(json);
            return Task.FromResult<WorldState?>(world);
        }
        catch
        {
            return Task.FromResult<WorldState?>(null);
        }
    }

    public bool HasSave(int slotIndex) =>
        File.Exists(GetSavePath(slotIndex));

    public void DeleteSave(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex)
    {
        var path = GetSavePath(slotIndex);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return WorldStateSerializer.DeserializeMetadata(json, slotIndex);
        }
        catch
        {
            return null;
        }
    }
}
