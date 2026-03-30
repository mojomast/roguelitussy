using System;
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
        if (!SaveSlots.IsValid(slotIndex) || world.Player is null)
        {
            return false;
        }

        try
        {
            var data = SaveSerializer.CreateSaveData(world, _utcNow());
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

    public async Task<WorldState?> LoadGame(int slotIndex)
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
            return errors.Count == 0 ? SaveSerializer.ToWorldState(data) : null;
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