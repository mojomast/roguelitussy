using System;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class EventBus : Node
{
    public event Action? TurnCompleted;
    public event Action<IAction>? PlayerActionSubmitted;
    public event Action<DamageResult>? DamageDealt;
    public event Action<EntityId>? EntityDied;
    public event Action<EntityId, StatusEffectInstance>? StatusEffectApplied;
    public event Action<EntityId, StatusEffectType>? StatusEffectRemoved;
    public event Action<int>? FloorChanged;
    public event Action<EntityId, Position, Position>? EntityMoved;
    public event Action<IEntity>? EntitySpawned;
    public event Action<EntityId>? EntityRemoved;
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

    public void EmitTurnCompleted() => TurnCompleted?.Invoke();

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
}
