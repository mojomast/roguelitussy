using Godot;

namespace Roguelike.Godot;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
    }

    // ═══ ENTITY MOVEMENT ═══
    [Signal]
    public delegate void EntityMovedEventHandler(
        string entityId, int fromX, int fromY, int toX, int toY);

    // ═══ COMBAT ═══
    [Signal]
    public delegate void EntityAttackedEventHandler(
        string attackerId, string defenderId, int damage, bool isCritical, bool isMiss);

    [Signal]
    public delegate void EntityHealthChangedEventHandler(
        string entityId, int oldHP, int newHP, int maxHP);

    [Signal]
    public delegate void EntityDiedEventHandler(string entityId, string killerEntityId);

    // ═══ TURN SYSTEM ═══
    [Signal]
    public delegate void TurnStartedEventHandler(int turnNumber);

    [Signal]
    public delegate void TurnEndedEventHandler(int turnNumber);

    [Signal]
    public delegate void EntityTurnStartedEventHandler(string entityId);

    // ═══ LEVEL / GENERATION ═══
    [Signal]
    public delegate void LevelGeneratedEventHandler(int depth, int width, int height);

    [Signal]
    public delegate void LevelTransitionEventHandler(int fromDepth, int toDepth);

    // ═══ ITEMS / INVENTORY ═══
    [Signal]
    public delegate void ItemPickedUpEventHandler(string entityId, string itemTemplateId);

    [Signal]
    public delegate void ItemUsedEventHandler(string entityId, string itemTemplateId, string effectDescription);

    [Signal]
    public delegate void ItemDroppedEventHandler(string entityId, string itemTemplateId, int posX, int posY);

    [Signal]
    public delegate void EquipmentChangedEventHandler(string entityId, int slot, string itemTemplateId);

    // ═══ FOV / VISIBILITY ═══
    [Signal]
    public delegate void FOVUpdatedEventHandler();

    // ═══ STATUS EFFECTS ═══
    [Signal]
    public delegate void StatusEffectAppliedEventHandler(string entityId, int effectType, int duration);

    [Signal]
    public delegate void StatusEffectRemovedEventHandler(string entityId, int effectType);

    // ═══ GAME FLOW ═══
    [Signal]
    public delegate void GameOverEventHandler(int finalDepth, int turnsSurvived);

    [Signal]
    public delegate void SaveRequestedEventHandler(int slotIndex);

    [Signal]
    public delegate void SaveCompletedEventHandler(bool success);

    [Signal]
    public delegate void LoadRequestedEventHandler(int slotIndex);

    [Signal]
    public delegate void LoadCompletedEventHandler(bool success);
}
