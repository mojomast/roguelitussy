using System;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class EventBus : Node
{
    public event Action<int>? TurnStarted;
    public event Action? TurnCompleted;
    public event Action<EntityId>? EntityTurnStarted;
    public event Action<IAction>? PlayerActionSubmitted;
    public event Action<DamageResult>? DamageDealt;
    public event Action<EntityId>? EntityDied;
    public event Action<EntityId, StatusEffectInstance>? StatusEffectApplied;
    public event Action<EntityId, StatusEffectType>? StatusEffectRemoved;
    public event Action<int>? FloorChanged;
    public event Action<EntityId, Position, Position>? EntityMoved;
    public event Action<IEntity>? EntitySpawned;
    public event Action<EntityId>? EntityRemoved;
    public event Action<Position>? TileChanged;
    public event Action<EntityId, ItemInstance>? ItemPickedUp;
    public event Action<EntityId, ItemInstance, Position>? ItemDropped;
    public event Action<string>? LogMessage;
    public event Action<EntityId>? InventoryChanged;
    public event Action<EntityId, int, int>? HPChanged;
    public event Action<int>? SaveRequested;
    public event Action<int>? LoadRequested;
    public event Action<bool>? SaveCompleted;
    public event Action<bool>? LoadCompleted;
    public event Action? FovRecalculated;
    public event Action<int, int, int>? LevelGenerated;
    public event Action<int, int>? LevelTransition;
    public event Action<EntityId, EquipSlot, ItemInstance?>? EquipmentChanged;
    public event Action<int, int>? GameOver;
    public event Action<EntityId, int, int>? ExperienceGained;
    public event Action<EntityId, int>? LeveledUp;
    public event Action<EntityId>? ProgressionChanged;
    public event Action<EntityId, int>? CurrencyChanged;

    public void EmitTurnStarted(int turnNumber) => TurnStarted?.Invoke(turnNumber);

    public void EmitTurnCompleted() => TurnCompleted?.Invoke();

    public void EmitEntityTurnStarted(EntityId entityId) => EntityTurnStarted?.Invoke(entityId);

    public void EmitPlayerActionSubmitted(IAction action) => PlayerActionSubmitted?.Invoke(action);

    public void EmitDamageDealt(DamageResult result) => DamageDealt?.Invoke(result);

    public void EmitEntityDied(EntityId entityId) => EntityDied?.Invoke(entityId);

    public void EmitStatusEffectApplied(EntityId entityId, StatusEffectInstance effect) =>
        StatusEffectApplied?.Invoke(entityId, effect);

    public void EmitStatusEffectRemoved(EntityId entityId, StatusEffectType effectType) =>
        StatusEffectRemoved?.Invoke(entityId, effectType);

    public void EmitFloorChanged(int depth) => FloorChanged?.Invoke(depth);

    public void EmitEntityMoved(EntityId entityId, Position from, Position to) =>
        EntityMoved?.Invoke(entityId, from, to);

    public void EmitEntitySpawned(IEntity entity) => EntitySpawned?.Invoke(entity);

    public void EmitEntityRemoved(EntityId entityId) => EntityRemoved?.Invoke(entityId);

    public void EmitTileChanged(Position position) => TileChanged?.Invoke(position);

    public void EmitItemPickedUp(EntityId entityId, ItemInstance item) => ItemPickedUp?.Invoke(entityId, item);

    public void EmitItemDropped(EntityId entityId, ItemInstance item, Position position) =>
        ItemDropped?.Invoke(entityId, item, position);

    public void EmitLogMessage(string message) => LogMessage?.Invoke(message);

    public void EmitInventoryChanged(EntityId entityId) => InventoryChanged?.Invoke(entityId);

    public void EmitHPChanged(EntityId entityId, int currentHp, int maxHp) =>
        HPChanged?.Invoke(entityId, currentHp, maxHp);

    public void EmitSaveRequested(int slot) => SaveRequested?.Invoke(slot);

    public void EmitLoadRequested(int slot) => LoadRequested?.Invoke(slot);

    public void EmitSaveCompleted(bool success) => SaveCompleted?.Invoke(success);

    public void EmitLoadCompleted(bool success) => LoadCompleted?.Invoke(success);

    public void EmitFovRecalculated() => FovRecalculated?.Invoke();

    public void EmitLevelGenerated(int depth, int width, int height) => LevelGenerated?.Invoke(depth, width, height);

    public void EmitLevelTransition(int fromDepth, int toDepth) => LevelTransition?.Invoke(fromDepth, toDepth);

    public void EmitEquipmentChanged(EntityId entityId, EquipSlot slot, ItemInstance? item) =>
        EquipmentChanged?.Invoke(entityId, slot, item);

    public void EmitGameOver(int finalDepth, int turnsSurvived) => GameOver?.Invoke(finalDepth, turnsSurvived);

    public void EmitExperienceGained(EntityId entityId, int amount, int total) => ExperienceGained?.Invoke(entityId, amount, total);

    public void EmitLeveledUp(EntityId entityId, int newLevel) => LeveledUp?.Invoke(entityId, newLevel);

    public void EmitProgressionChanged(EntityId entityId) => ProgressionChanged?.Invoke(entityId);

    public void EmitCurrencyChanged(EntityId entityId, int gold) => CurrencyChanged?.Invoke(entityId, gold);
}
