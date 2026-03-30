namespace Roguelike.Core;

public static class SaveSlots
{
    public const int Autosave = 0;
    public const int Slot1 = 1;
    public const int Slot2 = 2;
    public const int Slot3 = 3;
    public const int MaxSlotIndex = 3;

    public static bool IsValid(int slotIndex) => slotIndex is >= Autosave and <= MaxSlotIndex;

    public static string GetFileName(int slotIndex) =>
        slotIndex == Autosave ? "autosave.json" : $"slot_{slotIndex}.json";
}