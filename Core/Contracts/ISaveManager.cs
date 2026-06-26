using System.Threading.Tasks;

namespace Roguelike.Core;

public interface ISaveManager
{
    Task<bool> SaveGame(WorldState world, int slotIndex);

    Task<bool> SaveRun(SaveRunSnapshot snapshot, int slotIndex);

    Task<WorldState?> LoadGame(int slotIndex, IContentDatabase? content = null);

    Task<SaveRunSnapshot?> LoadRun(int slotIndex, IContentDatabase? content = null);

    bool HasSave(int slotIndex);

    void DeleteSave(int slotIndex);

    SaveMetadata? GetSaveMetadata(int slotIndex);
}
