using System.Threading.Tasks;

namespace Roguelike.Core;

public interface ISaveManager
{
    Task<bool> SaveGame(WorldState world, int slotIndex);
    Task<WorldState?> LoadGame(int slotIndex);
    bool HasSave(int slotIndex);
    void DeleteSave(int slotIndex);
    SaveMetadata? GetSaveMetadata(int slotIndex);
}
