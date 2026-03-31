using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class GameManager : Node
{
    private readonly Dictionary<int, WorldState> _cachedFloors = new();

    public sealed class CharacterCreationOptions
    {
        public static CharacterCreationOptions Default { get; } = new(
            "Rook",
            "Vanguard",
            "Survivor",
            "Iron Will",
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            new[] { "potion_health", "potion_health" },
            Array.Empty<string>());

        public CharacterCreationOptions(
            string name,
            string archetype,
            string origin,
            string trait,
            int bonusMaxHp,
            int bonusAttack,
            int bonusDefense,
            int bonusAccuracy,
            int bonusEvasion,
            int bonusSpeed,
            int bonusViewRadius,
            int inventoryCapacityBonus,
            IReadOnlyList<string> startingItemTemplateIds,
            IReadOnlyList<string> equippedItemTemplateIds)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Rook" : name;
            Archetype = string.IsNullOrWhiteSpace(archetype) ? "Vanguard" : archetype;
            Origin = string.IsNullOrWhiteSpace(origin) ? "Survivor" : origin;
            Trait = string.IsNullOrWhiteSpace(trait) ? "Iron Will" : trait;
            BonusMaxHp = bonusMaxHp;
            BonusAttack = bonusAttack;
            BonusDefense = bonusDefense;
            BonusAccuracy = bonusAccuracy;
            BonusEvasion = bonusEvasion;
            BonusSpeed = bonusSpeed;
            BonusViewRadius = bonusViewRadius;
            InventoryCapacityBonus = inventoryCapacityBonus;
            StartingItemTemplateIds = new List<string>(startingItemTemplateIds ?? Array.Empty<string>());
            EquippedItemTemplateIds = new List<string>(equippedItemTemplateIds ?? Array.Empty<string>());
        }

        public string Name { get; }

        public string Archetype { get; }

        public string Origin { get; }

        public string Trait { get; }

        public int BonusMaxHp { get; }

        public int BonusAttack { get; }

        public int BonusDefense { get; }

        public int BonusAccuracy { get; }

        public int BonusEvasion { get; }

        public int BonusSpeed { get; }

        public int BonusViewRadius { get; }

        public int InventoryCapacityBonus { get; }

        public IReadOnlyList<string> StartingItemTemplateIds { get; }

        public IReadOnlyList<string> EquippedItemTemplateIds { get; }
    }

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

    public CharacterCreationOptions CharacterOptions { get; private set; } = CharacterCreationOptions.Default;

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
        EnsureRuntimeServices();
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

    public void SetCharacterCreationOptions(CharacterCreationOptions? options)
    {
        CharacterOptions = options ?? CharacterCreationOptions.Default;
    }

    public void SetRuntimeContent(IContentDatabase content)
    {
        Content = content;
        GetNodeOrNull<ContentDatabase>("/root/ContentDatabase")?.SetDatabase(content);
    }

    public IReadOnlyList<string> ReloadContentDatabase(string? contentDirectory = null)
    {
        try
        {
            var resolvedDirectory = ToolPaths.ResolveContentDirectory(contentDirectory);
            var loader = ContentLoader.LoadFromDirectory(resolvedDirectory, throwOnValidationErrors: false);
            if (!loader.IsValid)
            {
                return loader.ValidationErrors;
            }

            SetRuntimeContent(loader);
            Bus?.EmitLogMessage($"Reloaded content from {loader.ContentDirectory}.");
            return Array.Empty<string>();
        }
        catch (Exception ex)
        {
            return new[] { ex.Message };
        }
    }

    public bool SaveToSlot(int slot)
    {
        if (World is null || SaveManager is null)
        {
            Bus?.EmitSaveCompleted(false);
            Bus?.EmitLogMessage($"Save failed for slot {slot}.");
            return false;
        }

        var success = SaveManager.SaveGame(World, slot).GetAwaiter().GetResult();
        Bus?.EmitSaveCompleted(success);
        Bus?.EmitLogMessage(success ? $"Saved slot {slot}." : $"Save failed for slot {slot}.");
        return success;
    }

    public bool LoadFromSlot(int slot)
    {
        if (SaveManager is null || !SaveManager.HasSave(slot))
        {
            Bus?.EmitLoadCompleted(false);
            Bus?.EmitLogMessage($"Load failed for slot {slot}.");
            return false;
        }

        CurrentState = GameState.Loading;
        var world = SaveManager.LoadGame(slot).GetAwaiter().GetResult();
        if (world is null)
        {
            CurrentState = GameState.MainMenu;
            Bus?.EmitLoadCompleted(false);
            Bus?.EmitLogMessage($"Load failed for slot {slot}.");
            return false;
        }

        if (world.Player is not null)
        {
            Seed = world.Seed;
            _cachedFloors.Clear();
        }

        Scheduler = new TurnScheduler();
        LoadWorld(world);
        Bus?.EmitLoadCompleted(true);
        Bus?.EmitTurnCompleted();
        Bus?.EmitLogMessage($"Loaded slot {slot}.");
        return true;
    }

    public bool SetMapReveal(bool revealAll)
    {
        if (World is null)
        {
            return false;
        }

        if (revealAll)
        {
            for (var y = 0; y < World.Height; y++)
            {
                for (var x = 0; x < World.Width; x++)
                {
                    World.SetVisible(new Position(x, y), true);
                }
            }

            Bus?.EmitFovRecalculated();
            return true;
        }

        RecalculatePlayerVisibility(World);
        return true;
    }

    public bool TeleportPlayer(Position target)
    {
        if (World?.Player is null)
        {
            return false;
        }

        if (!World.InBounds(target) || !World.IsWalkable(target))
        {
            return false;
        }

        var player = World.Player;
        if (World.GetEntityAt(target) is not null && target != player.Position)
        {
            return false;
        }

        var origin = player.Position;
        if (!World.MoveEntity(player.Id, target))
        {
            return false;
        }

        Bus?.EmitEntityMoved(player.Id, origin, target);
        RecalculatePlayerVisibility(World);
        Bus?.EmitLogMessage($"Teleported player to {target.X},{target.Y}.");
        return true;
    }

    public bool TravelToFloor(int targetFloor)
    {
        if (targetFloor < 0)
        {
            return false;
        }

        if (World?.Player is null)
        {
            return false;
        }

        if (targetFloor == CurrentFloor)
        {
            Bus?.EmitLogMessage($"Already on floor {targetFloor}.");
            return true;
        }

        return TryTravelToFloor(targetFloor);
    }

    public void LoadToolWorld(WorldState world, string? logMessage = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        _cachedFloors.Clear();
        Seed = world.Seed;
        CurrentFloor = world.Depth;
        Scheduler = new TurnScheduler();
        LoadWorld(world);

        if (!string.IsNullOrWhiteSpace(logMessage))
        {
            Bus?.EmitLogMessage(logMessage);
        }
    }

    public void StartNewGame(int seed)
    {
        Seed = seed;
        CurrentFloor = 0;
        _cachedFloors.Clear();

        if (Generator is null || Content is null)
        {
            CurrentState = GameState.MainMenu;
            Bus?.EmitLogMessage("Cannot start a new game because runtime services are not initialized.");
            return;
        }

        try
        {
            var world = CreateGeneratedWorld(seed, CurrentFloor);
            var player = CreatePlayer(FindArrivalPosition(world, TileType.StairsUp), new Random(MixSeed(seed, CurrentFloor) ^ 17));
            PlacePlayerInWorld(world, player, TileType.StairsUp);
            LoadWorld(world);
            Bus?.EmitLogMessage($"Starting new game with seed {seed}.");
        }
        catch (Exception ex)
        {
            CurrentState = GameState.MainMenu;
            Bus?.EmitLogMessage($"Failed to start a new game: {ex.Message}");
        }
    }

    public void LoadWorld(WorldState world)
    {
        World = world;
        CurrentFloor = world.Depth;
        CurrentState = GameState.Playing;
        RecalculatePlayerVisibility(world);
        Bus?.EmitLevelGenerated(world.Depth, world.Width, world.Height);
        Bus?.EmitFloorChanged(CurrentFloor);
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

        var changedFloor = false;
        if (outcome.Result == ActionResult.Success && action.ActorId == playerId)
        {
            changedFloor = action.Type switch
            {
                ActionType.Descend => TryTravelToFloor(CurrentFloor + 1),
                ActionType.Ascend when CurrentFloor > 0 => TryTravelToFloor(CurrentFloor - 1),
                _ => false,
            };
        }

        if (!changedFloor)
        {
            RecalculatePlayerVisibility(World);
            EmitStateDelta(action, actorPositionBefore, inventoryBefore, equipmentBefore, playerId);
        }

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

    private void OnSaveRequested(int slot) => SaveToSlot(slot);

    private void OnLoadRequested(int slot) => LoadFromSlot(slot);

    private void EnsureRuntimeServices()
    {
        if (Generator is not null && Scheduler is not null && Content is not null && SaveManager is not null)
        {
            return;
        }

        try
        {
            var content = Content as ContentLoader ?? ContentLoader.LoadFromRepository(Directory.GetCurrentDirectory());
            GetNodeOrNull<ContentDatabase>("/root/ContentDatabase")?.SetDatabase(content);

            AttachServices(
                World ?? new WorldState(),
                Scheduler ?? new TurnScheduler(),
                Generator ?? new DungeonGenerator(),
                Fov ?? new FOVCalculator(),
                content,
                SaveManager ?? new SaveManager(),
                Bus);
        }
        catch (Exception ex)
        {
            Bus?.EmitLogMessage($"Runtime initialization failed: {ex.Message}");
        }
    }

    private WorldState CreateGeneratedWorld(int seed, int depth)
    {
        if (Generator is null || Content is null)
        {
            throw new InvalidOperationException("Generator and content services must be initialized before creating a world.");
        }

        var world = new WorldState();
        var level = Generator.GenerateLevel(world, seed, depth);
        var previousTurnNumber = World?.TurnNumber ?? 0;
        var rng = new Random(MixSeed(seed, depth));

        world.TurnNumber = previousTurnNumber;
        PopulateWorld(world, level, rng);
        return world;
    }

    private void PopulateWorld(WorldState world, LevelData level, Random rng)
    {
        if (Content is null)
        {
            throw new InvalidOperationException("Content service is not available.");
        }

        var enemies = Content.GetAvailableEnemies(world.Depth);
        foreach (var spawn in level.EnemySpawns)
        {
            if (enemies.Count == 0)
            {
                break;
            }

            var template = SelectEnemyTemplate(enemies, rng);
            var enemy = new Entity(
                template.DisplayName,
                spawn,
                template.BaseStats.Clone(),
                template.Faction,
                id: EntityId.NewSeeded(rng));
            enemy.SetComponent<IBrain>(BrainFactory.Create(template));
            world.AddEntity(enemy);
        }

        foreach (var spawn in level.ItemSpawns)
        {
            foreach (var item in CreateFloorItems(world.Depth, rng))
            {
                world.DropItem(spawn, item);
            }
        }
    }

    private Entity CreatePlayer(Position spawn, Random rng)
    {
        var character = CharacterOptions;
        var stats = new Stats
        {
            HP = 40,
            MaxHP = 40,
            Attack = 8,
            Accuracy = 80,
            Defense = 3,
            Evasion = 10,
            Speed = 100,
            ViewRadius = 8,
        };

        stats.MaxHP += character.BonusMaxHp;
        stats.HP = stats.MaxHP;
        stats.Attack += character.BonusAttack;
        stats.Accuracy += character.BonusAccuracy;
        stats.Defense += character.BonusDefense;
        stats.Evasion += character.BonusEvasion;
        stats.Speed += character.BonusSpeed;
        stats.ViewRadius += character.BonusViewRadius;

        var player = new Entity(
            character.Name,
            spawn,
            stats,
            Faction.Player,
            id: EntityId.NewSeeded(rng));

        var inventory = new InventoryComponent(Math.Max(8, 20 + character.InventoryCapacityBonus));
        foreach (var templateId in character.StartingItemTemplateIds)
        {
            var item = new ItemInstance
            {
                InstanceId = EntityId.NewSeeded(rng),
                TemplateId = templateId,
                StackCount = 1,
                IsIdentified = true,
            };

            if (Content is not null && Content.TryGetItemTemplate(templateId, out var template) && template.MaxStack > 1)
            {
                inventory.AddWithStacking(item, template.MaxStack);
            }
            else
            {
                inventory.Add(item);
            }
        }

        player.SetComponent(inventory);
        EquipStartingLoadout(player, inventory, character);
        return player;
    }

    private void EquipStartingLoadout(IEntity player, InventoryComponent inventory, CharacterCreationOptions character)
    {
        var content = Content;
        if (content is null)
        {
            return;
        }

        foreach (var templateId in character.EquippedItemTemplateIds)
        {
            if (!content.TryGetItemTemplate(templateId, out var template) || template.Slot == EquipSlot.None)
            {
                continue;
            }

            ItemInstance? item = null;
            foreach (var candidate in inventory.Items)
            {
                if (candidate.TemplateId == templateId && !inventory.IsEquipped(candidate.InstanceId))
                {
                    item = candidate;
                    break;
                }
            }

            if (item is null || !inventory.TryEquip(item, template.Slot, template.StatModifiers, out var previous))
            {
                continue;
            }

            if (previous is not null)
            {
                ApplyStatModifiers(player.Stats, previous.StatModifiers, -1);
            }

            ApplyStatModifiers(player.Stats, template.StatModifiers, 1);
        }
    }

    private IReadOnlyList<ItemInstance> CreateFloorItems(int depth, Random rng)
    {
        var content = Content ?? throw new InvalidOperationException("Content service is not available.");

        if (content is ContentLoader loader)
        {
            try
            {
                var rolls = LootTableResolver.RollTable(loader, "floor_loot", rng, depth);
                var items = new List<ItemInstance>(rolls.Count);
                foreach (var roll in rolls)
                {
                    items.Add(new ItemInstance
                    {
                        InstanceId = EntityId.NewSeeded(rng),
                        TemplateId = roll.ItemId,
                        StackCount = Math.Max(1, roll.Count),
                        IsIdentified = false,
                    });
                }

                if (items.Count > 0)
                {
                    return items;
                }
            }
            catch
            {
            }
        }

        var templates = content.GetAvailableItems(depth);
        if (templates.Count == 0)
        {
            return Array.Empty<ItemInstance>();
        }

        var template = templates[rng.Next(templates.Count)];
        return new[]
        {
            new ItemInstance
            {
                InstanceId = EntityId.NewSeeded(rng),
                TemplateId = template.TemplateId,
                StackCount = 1,
                IsIdentified = false,
            },
        };
    }

    private EnemyTemplate SelectEnemyTemplate(IReadOnlyList<EnemyTemplate> templates, Random rng)
    {
        var totalWeight = 0;
        for (var i = 0; i < templates.Count; i++)
        {
            totalWeight += Math.Max(1, templates[i].SpawnWeight);
        }

        var roll = rng.Next(totalWeight);
        var cumulative = 0;
        for (var i = 0; i < templates.Count; i++)
        {
            cumulative += Math.Max(1, templates[i].SpawnWeight);
            if (roll < cumulative)
            {
                return templates[i];
            }
        }

        return templates[^1];
    }

    private bool TryTravelToFloor(int targetFloor)
    {
        if (World?.Player is null)
        {
            return false;
        }

        try
        {
            var previousFloor = CurrentFloor;
            var currentWorld = World;
            var player = currentWorld.Player;
            var targetWorld = _cachedFloors.TryGetValue(targetFloor, out var cachedFloor)
                ? cachedFloor
                : CreateGeneratedWorld(Seed, targetFloor);

            _cachedFloors[targetFloor] = targetWorld;
            currentWorld.RemoveEntity(player.Id);
            _cachedFloors[previousFloor] = currentWorld;

            var arrivalTile = targetFloor > previousFloor ? TileType.StairsUp : TileType.StairsDown;
            PlacePlayerInWorld(targetWorld, player, arrivalTile);
            Scheduler = new TurnScheduler();
            LoadWorld(targetWorld);
            Bus?.EmitLevelTransition(previousFloor, targetFloor);
            Bus?.EmitLogMessage($"Travelled to floor {targetFloor}.");
            return true;
        }
        catch (Exception ex)
        {
            Bus?.EmitLogMessage($"Floor transition failed: {ex.Message}");
            return false;
        }
    }

    private static int MixSeed(int seed, int depth)
    {
        unchecked
        {
            return seed ^ (depth * 7919) ^ 0x5f3759df;
        }
    }

    private static Position FindArrivalPosition(IWorldState world, TileType tileType)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) == tileType)
                {
                    return position;
                }
            }
        }

        throw new InvalidOperationException($"No tile of type {tileType} exists in the destination floor.");
    }

    private static void PlacePlayerInWorld(WorldState world, IEntity player, TileType arrivalTile)
    {
        if (world.GetEntity(player.Id) is not null)
        {
            world.RemoveEntity(player.Id);
        }

        player.Position = FindArrivalPosition(world, arrivalTile);
        player.Stats.Energy = 0;
        world.Player = player;
        world.AddEntity(player);
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

    private void RecalculatePlayerVisibility(WorldState? world)
    {
        if (world is null)
        {
            return;
        }

        world.ClearVisibility();

        var player = world.GetEntity(world.Player.Id);
        if (player is null)
        {
            Bus?.EmitFovRecalculated();
            return;
        }

        var fov = Fov ??= new FOVCalculator();
        fov.Compute(
            player.Position,
            System.Math.Max(0, player.Stats.ViewRadius),
            position => !world.InBounds(position) || world.BlocksSight(position),
            position =>
            {
                if (world.InBounds(position))
                {
                    world.SetVisible(position, true);
                }
            });

        world.SetVisible(player.Position, true);
        Bus?.EmitFovRecalculated();
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

                if (inventoryBefore.Count != inventoryAfter.Items.Count
                    || action.Type is ActionType.UseItem or ActionType.ToggleEquip or ActionType.DropItem or ActionType.PickupItem)
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

    private static void ApplyStatModifiers(Stats stats, IReadOnlyDictionary<string, int> modifiers, int direction)
    {
        foreach (var modifier in modifiers)
        {
            var value = modifier.Value * direction;
            switch (modifier.Key.ToLowerInvariant())
            {
                case "hp":
                    stats.HP += value;
                    break;
                case "max_hp":
                case "maxhp":
                    stats.MaxHP += value;
                    stats.HP = Math.Min(stats.HP, stats.MaxHP);
                    break;
                case "attack":
                    stats.Attack += value;
                    break;
                case "accuracy":
                    stats.Accuracy += value;
                    break;
                case "defense":
                    stats.Defense += value;
                    break;
                case "evasion":
                    stats.Evasion += value;
                    break;
                case "speed":
                    stats.Speed += value;
                    break;
                case "view_radius":
                case "viewradius":
                    stats.ViewRadius += value;
                    break;
            }
        }
    }
}
