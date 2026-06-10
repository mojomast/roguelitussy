using System.Threading.Tasks;

namespace Roguelike.Core;

public interface ISaveManager
{
    Task<bool> SaveGame(WorldState world, int slotIndex);

    Task<bool> SaveRun(SaveRunSnapshot snapshot, int slotIndex);

    Task<WorldState?> LoadGame(int slotIndex);

    Task<SaveRunSnapshot?> LoadRun(int slotIndex);

    bool HasSave(int slotIndex);

    void DeleteSave(int slotIndex);

    SaveMetadata? GetSaveMetadata(int slotIndex);
}
