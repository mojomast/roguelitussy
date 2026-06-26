using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Roguelike.Core;

public sealed class SaveManager : ISaveManager
{
    private readonly string _saveDirectory;
    private readonly Func<DateTime> _utcNow;

    public SaveManager(string? saveDirectory = null, Func<DateTime>? utcNow = null)
    {
        _saveDirectory = saveDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "godotussy",
            "saves");
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        if (world.Player is null)
        {
            return false;
        }

        return await SaveRun(new SaveRunSnapshot(world.Seed, world.Depth, world, new Dictionary<int, WorldState> { [world.Depth] = world }), slotIndex).ConfigureAwait(false);
    }

    public async Task<bool> SaveRun(SaveRunSnapshot snapshot, int slotIndex)
    {
        if (!SaveSlots.IsValid(slotIndex) || snapshot.ActiveWorld.Player is null)
        {
            return false;
        }

        try
        {
            var data = SaveSerializer.CreateSaveData(snapshot, _utcNow());
            var errors = SaveValidator.Validate(data);
            if (errors.Count > 0)
            {
                return false;
            }

            Directory.CreateDirectory(_saveDirectory);
            var path = GetSavePath(slotIndex);
            var tempPath = path + ".tmp";
            var json = SaveSerializer.ToJson(data);

            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8).ConfigureAwait(false);
            File.Move(tempPath, path, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<WorldState?> LoadGame(int slotIndex, IContentDatabase? content = null)
    {
        var snapshot = await LoadRun(slotIndex, content).ConfigureAwait(false);
        return snapshot?.ActiveWorld;
    }

    public async Task<SaveRunSnapshot?> LoadRun(int slotIndex, IContentDatabase? content = null)
    {
        if (!SaveSlots.IsValid(slotIndex))
        {
            return null;
        }

        var path = GetSavePath(slotIndex);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var data = SaveMigrator.MigrateToCurrent(json);
            var errors = SaveValidator.Validate(data);
            return errors.Count == 0 ? SaveSerializer.ToRunSnapshot(data, content) : null;
        }
        catch
        {
            return null;
        }
    }

    public bool HasSave(int slotIndex) => SaveSlots.IsValid(slotIndex) && File.Exists(GetSavePath(slotIndex));

    public void DeleteSave(int slotIndex)
    {
        if (!SaveSlots.IsValid(slotIndex))
        {
            return;
        }

        var path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex)
    {
        if (!SaveSlots.IsValid(slotIndex))
        {
            return null;
        }

        var path = GetSavePath(slotIndex);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = SaveMigrator.MigrateToCurrent(json);
            var errors = SaveValidator.Validate(data);
            return errors.Count == 0 ? SaveSerializer.ToMetadata(data, slotIndex) : null;
        }
        catch
        {
            return null;
        }
    }

    private string GetSavePath(int slotIndex) => Path.Combine(_saveDirectory, SaveSlots.GetFileName(slotIndex));
}
