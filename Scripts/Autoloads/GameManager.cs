using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class GameManager : Node
{
    private sealed class OneShotScheduler : ITurnScheduler
    {
        private readonly ITurnScheduler _inner;
        private readonly EntityId _actorId;
        private WorldState? _world;
        private bool _consumed;

        public OneShotScheduler(ITurnScheduler inner, EntityId actorId)
        {
            _inner = inner;
            _actorId = actorId;
        }

        public int EnergyThreshold => _inner.EnergyThreshold;

        public void BeginRound(WorldState world)
        {
            _world = world;
            _consumed = false;
            _inner.BeginRound(world);
        }

        public bool HasNextActor() => !_consumed && _world?.GetEntity(_actorId) is { IsAlive: true };

        public IEntity? GetNextActor() => _consumed || _world is null ? null : _world.GetEntity(_actorId);

        public void ConsumeEnergy(EntityId actorId, int cost)
        {
            _consumed = true;
            _inner.ConsumeEnergy(actorId, cost);
        }

        public void EndRound(WorldState world)
        {
            _inner.EndRound(world);
            _world = null;
        }

        public void Register(IEntity entity) => _inner.Register(entity);

        public void Unregister(EntityId id) => _inner.Unregister(id);
    }

    private readonly GameLoop _gameLoop = new();
    private readonly IPathfinder _pathfinder = new Pathfinder();

    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Loading,
    }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    public int CurrentFloor { get; private set; }

    public int Seed { get; private set; }

    public WorldState? World { get; private set; }

    public ITurnScheduler? Scheduler { get; private set; }

    public IGenerator? Generator { get; private set; }

    public IFOV? Fov { get; private set; }

    public IContentDatabase? Content { get; private set; }

    public ISaveManager? SaveManager { get; private set; }

    public EventBus? Bus { get; private set; }

    public override void _Ready()
    {
        BindBus(Bus ?? GetNodeOrNull<EventBus>("/root/EventBus"));
    }

    public void AttachServices(
        WorldState world,
        ITurnScheduler scheduler,
        IGenerator generator,
        IFOV fov,
        IContentDatabase content,
        ISaveManager saveManager,
        EventBus? bus = null)
    {
        World = world;
        Scheduler = scheduler;
        Generator = generator;
        Fov = fov;
        Content = content;
        SaveManager = saveManager;
        BindBus(bus ?? Bus);
    }

    public void StartNewGame(int seed)
    {
        Seed = seed;
        CurrentFloor = 0;
        CurrentState = GameState.Playing;
        Bus?.EmitFloorChanged(CurrentFloor);
    }

    public void LoadWorld(WorldState world)
    {
        World = world;
        CurrentFloor = world.Depth;
        CurrentState = GameState.Playing;
        Bus?.EmitLevelGenerated(world.Depth, world.Width, world.Height);
        EmitWorldSnapshot(world);
    }

    public ActionOutcome ProcessPlayerAction(IAction action)
    {
        if (World is null || Scheduler is null)
        {
            return ActionOutcome.Fail(ActionResult.Invalid);
        }

        var validation = action.Validate(World);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actorBefore = World.GetEntity(action.ActorId);
        var actorPositionBefore = actorBefore?.Position ?? Position.Invalid;
        var playerId = World.Player.Id;
        var inventoryBefore = SnapshotInventory(actorBefore?.GetComponent<InventoryComponent>());
        var equipmentBefore = SnapshotEquipment(actorBefore?.GetComponent<InventoryComponent>());

        Bus?.EmitTurnStarted(World.TurnNumber + 1);

        var playerActionConsumed = false;
        var outcome = _gameLoop.ProcessRound(
            World,
            new OneShotScheduler(Scheduler, action.ActorId),
            actor =>
            {
                Bus?.EmitEntityTurnStarted(actor.Id);
                if (!playerActionConsumed && actor.Id == action.ActorId)
                {
                    playerActionConsumed = true;
                    return action;
                }

                var brain = actor.GetComponent<IBrain>();
                return brain?.DecideAction(actor, World, _pathfinder) ?? new WaitAction(actor.Id);
            });

        foreach (var combatEvent in outcome.CombatEvents)
        {
            foreach (var damage in combatEvent.DamageResults)
            {
                Bus?.EmitDamageDealt(damage);
                var defender = World.GetEntity(damage.DefenderId);
                if (defender is not null)
                {
                    Bus?.EmitHPChanged(defender.Id, defender.Stats.HP, defender.Stats.MaxHP);
                }

                if (damage.IsKill)
                {
                    Bus?.EmitEntityDied(damage.DefenderId);
                    if (damage.DefenderId == playerId)
                    {
                        CurrentState = GameState.GameOver;
                        Bus?.EmitGameOver(CurrentFloor, World.TurnNumber);
                    }
                }
            }

            foreach (var effect in combatEvent.StatusEffectsApplied)
            {
                Bus?.EmitStatusEffectApplied(action.ActorId, effect);
            }
        }

        EmitStateDelta(action, actorPositionBefore, inventoryBefore, equipmentBefore, playerId);

        foreach (var message in outcome.LogMessages)
        {
            Bus?.EmitLogMessage(message);
        }

        Bus?.EmitTurnCompleted();
        return outcome;
    }

    public void TransitionFloor(int newFloor)
    {
        var previousFloor = CurrentFloor;
        CurrentFloor = newFloor;
        Bus?.EmitLevelTransition(previousFloor, newFloor);
        Bus?.EmitFloorChanged(newFloor);
    }

    private void BindBus(EventBus? eventBus)
    {
        if (ReferenceEquals(Bus, eventBus))
        {
            return;
        }

        if (Bus is not null)
        {
            Bus.PlayerActionSubmitted -= OnPlayerActionSubmitted;
            Bus.SaveRequested -= OnSaveRequested;
            Bus.LoadRequested -= OnLoadRequested;
        }

        Bus = eventBus;
        if (Bus is null)
        {
            return;
        }

        Bus.PlayerActionSubmitted += OnPlayerActionSubmitted;
        Bus.SaveRequested += OnSaveRequested;
        Bus.LoadRequested += OnLoadRequested;
    }

    private void OnPlayerActionSubmitted(IAction action)
    {
        ProcessPlayerAction(action);
    }

    private async void OnSaveRequested(int slot)
    {
        var success = World is not null && SaveManager is not null && await SaveManager.SaveGame(World, slot).ConfigureAwait(false);
        Bus?.EmitSaveCompleted(success);
        Bus?.EmitLogMessage(success ? $"Saved slot {slot}." : $"Save failed for slot {slot}.");
    }

    private async void OnLoadRequested(int slot)
    {
        if (SaveManager is null)
        {
            Bus?.EmitLoadCompleted(false);
            Bus?.EmitLogMessage($"Load failed for slot {slot}.");
            return;
        }

        CurrentState = GameState.Loading;
        var world = await SaveManager.LoadGame(slot).ConfigureAwait(false);
        if (world is null)
        {
            CurrentState = GameState.MainMenu;
            Bus?.EmitLoadCompleted(false);
            Bus?.EmitLogMessage($"Load failed for slot {slot}.");
            return;
        }

        LoadWorld(world);
        Bus?.EmitFloorChanged(CurrentFloor);
        Bus?.EmitLoadCompleted(true);
        Bus?.EmitTurnCompleted();
        Bus?.EmitLogMessage($"Loaded slot {slot}.");
    }

    private void EmitWorldSnapshot(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            Bus?.EmitEntitySpawned(entity);
            Bus?.EmitHPChanged(entity.Id, entity.Stats.HP, entity.Stats.MaxHP);
        }

        Bus?.EmitInventoryChanged(world.Player.Id);
    }

    private void EmitStateDelta(
        IAction action,
        Position actorPositionBefore,
        HashSet<EntityId> inventoryBefore,
        Dictionary<EquipSlot, EntityId> equipmentBefore,
        EntityId playerId)
    {
        if (World is null)
        {
            return;
        }

        var actorAfter = World.GetEntity(action.ActorId);
        if (actorAfter is not null)
        {
            if (actorPositionBefore != Position.Invalid && actorPositionBefore != actorAfter.Position)
            {
                Bus?.EmitEntityMoved(action.ActorId, actorPositionBefore, actorAfter.Position);
            }

            Bus?.EmitHPChanged(actorAfter.Id, actorAfter.Stats.HP, actorAfter.Stats.MaxHP);

            var inventoryAfter = actorAfter.GetComponent<InventoryComponent>();
            if (inventoryAfter is not null)
            {
                foreach (var item in inventoryAfter.Items)
                {
                    if (!inventoryBefore.Contains(item.InstanceId))
                    {
                        Bus?.EmitItemPickedUp(actorAfter.Id, item);
                    }
                }

                if (inventoryBefore.Count != inventoryAfter.Items.Count || action.Type == ActionType.UseItem)
                {
                    Bus?.EmitInventoryChanged(actorAfter.Id);
                }

                foreach (var pair in inventoryAfter.EquippedItems)
                {
                    if (!equipmentBefore.TryGetValue(pair.Key, out var previousItemId) || previousItemId != pair.Value.Item.InstanceId)
                    {
                        Bus?.EmitEquipmentChanged(actorAfter.Id, pair.Key, pair.Value.Item);
                    }
                }

                foreach (var pair in equipmentBefore)
                {
                    if (!inventoryAfter.EquippedItems.ContainsKey(pair.Key))
                    {
                        Bus?.EmitEquipmentChanged(actorAfter.Id, pair.Key, null);
                    }
                }
            }
        }
        else if (actorPositionBefore != Position.Invalid)
        {
            Bus?.EmitEntityDied(action.ActorId);
        }

        if (playerId != action.ActorId && World.GetEntity(playerId) is { } player)
        {
            Bus?.EmitHPChanged(player.Id, player.Stats.HP, player.Stats.MaxHP);
        }
    }

    private static HashSet<EntityId> SnapshotInventory(InventoryComponent? inventory)
    {
        var snapshot = new HashSet<EntityId>();
        if (inventory is null)
        {
            return snapshot;
        }

        foreach (var item in inventory.Items)
        {
            snapshot.Add(item.InstanceId);
        }

        return snapshot;
    }

    private static Dictionary<EquipSlot, EntityId> SnapshotEquipment(InventoryComponent? inventory)
    {
        var snapshot = new Dictionary<EquipSlot, EntityId>();
        if (inventory is null)
        {
            return snapshot;
        }

        foreach (var pair in inventory.EquippedItems)
        {
            snapshot[pair.Key] = pair.Value.Item.InstanceId;
        }

        return snapshot;
    }
}
