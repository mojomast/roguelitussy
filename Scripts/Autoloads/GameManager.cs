using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class GameManager : Node
{
    private readonly Dictionary<int, WorldState> _cachedFloors = new();
    private readonly Dictionary<int, FloorEntrances> _floorEntrances = new();
    private const int StartingGold = 120;

    private sealed record FloorEntrances(Position StairsUp, Position StairsDown);

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
            Array.Empty<string>(),
            "human",
            "neutral",
            "default");

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
            IReadOnlyList<string> equippedItemTemplateIds,
            string raceId = "human",
            string genderId = "neutral",
            string appearanceId = "default")
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
            RaceId = string.IsNullOrWhiteSpace(raceId) ? "human" : raceId;
            GenderId = string.IsNullOrWhiteSpace(genderId) ? "neutral" : genderId;
            AppearanceId = string.IsNullOrWhiteSpace(appearanceId) ? "default" : appearanceId;
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

        public string RaceId { get; }

        public string GenderId { get; }

        public string AppearanceId { get; }
    }

    public sealed record InteractionContext(
        EntityId NpcId,
        string DisplayName,
        string Role,
        bool IsMerchant,
        NpcTemplate NpcTemplate,
        DialogueTemplate DialogueTemplate);

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

    private sealed class NextActorScheduler : ITurnScheduler
    {
        private readonly ITurnScheduler _inner;
        private readonly Predicate<IEntity> _predicate;
        private IEntity? _nextActor;
        private bool _consumed;

        public NextActorScheduler(ITurnScheduler inner, Predicate<IEntity> predicate)
        {
            _inner = inner;
            _predicate = predicate;
        }

        public int EnergyThreshold => _inner.EnergyThreshold;

        public void BeginRound(WorldState world)
        {
            _consumed = false;
            _nextActor = null;
            _inner.BeginRound(world);

            var candidate = _inner.GetNextActor();
            if (candidate is not null && candidate.IsAlive && _predicate(candidate))
            {
                _nextActor = candidate;
            }
        }

        public bool HasNextActor() => !_consumed && _nextActor is not null;

        public IEntity? GetNextActor() => _consumed ? null : _nextActor;

        public void ConsumeEnergy(EntityId actorId, int cost)
        {
            _consumed = true;
            _inner.ConsumeEnergy(actorId, cost);
        }

        public void EndRound(WorldState world)
        {
            _inner.EndRound(world);
            _nextActor = null;
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
        BindBus(ResolveEventBus());
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
        World.ContentDatabase = content;
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
        ResolveContentAutoload()?.SetDatabase(content);
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
        EnsureRuntimeServices();

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
            _floorEntrances.Clear();
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
        _floorEntrances.Clear();
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
        EnsureRuntimeServices();
        Seed = seed;
        CurrentFloor = 0;
        _cachedFloors.Clear();
        _floorEntrances.Clear();

        if (Generator is null || Content is null)
        {
            CurrentState = GameState.MainMenu;
            Bus?.EmitLogMessage("Cannot start a new game because runtime services are not initialized.");
            return;
        }

        try
        {
            var generatedFloor = CreateGeneratedWorld(seed, CurrentFloor);
            var world = generatedFloor.World;
            _floorEntrances[CurrentFloor] = generatedFloor.Entrances;

            var player = CreatePlayer(generatedFloor.Entrances.StairsUp, new Random(MixSeed(seed, CurrentFloor) ^ 17));
            PlacePlayerInWorld(world, player, generatedFloor.Entrances.StairsUp, TileType.StairsUp);
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
        world.ContentDatabase = Content;
        RememberFloorEntrances(world);
        World = world;
        CurrentFloor = world.Depth;
        CurrentState = GameState.Playing;
        RegisterWorldEntities(world);
        RecalculatePlayerVisibility(world);
        Bus?.EmitLevelGenerated(world.Depth, world.Width, world.Height);
        Bus?.EmitFloorChanged(CurrentFloor);
        EmitWorldSnapshot(world);
    }

    public InteractionContext? GetInteractionContext()
    {
        if (World?.Player is null || Content is null)
        {
            return null;
        }

        var entity = FindAdjacentNpc(World);
        var npc = entity?.GetComponent<NpcComponent>();
        if (entity is null || npc is null)
        {
            return null;
        }

        if (!Content.TryGetNpcTemplate(npc.TemplateId, out var npcTemplate)
            || !Content.TryGetDialogueTemplate(npc.DialogueId, out var dialogueTemplate))
        {
            return null;
        }

        return new InteractionContext(
            entity.Id,
            npcTemplate.DisplayName,
            npcTemplate.Role,
            entity.GetComponent<MerchantComponent>() is { Offers.Count: > 0 },
            npcTemplate,
            dialogueTemplate);
    }

    public IReadOnlyList<PerkTemplate> GetAvailablePerkChoices()
    {
        return World?.Player is null ? Array.Empty<PerkTemplate>() : ProgressionService.GetAvailablePerkChoices(World.Player, Content);
    }

    public bool TrySpendStatPoint(string statName, out string message)
    {
        message = string.Empty;
        if (World?.Player is null)
        {
            message = "No active player is available.";
            return false;
        }

        var player = World.Player;
        if (!ProgressionService.TrySpendStatPoint(player, statName, out message))
        {
            return false;
        }

        Bus?.EmitLogMessage(message);
        Bus?.EmitHPChanged(player.Id, player.Stats.HP, player.Stats.MaxHP);
        Bus?.EmitProgressionChanged(player.Id);
        return true;
    }

    public bool TrySelectPerk(string perkId, out string message)
    {
        message = string.Empty;
        if (World?.Player is null)
        {
            message = "No active player is available.";
            return false;
        }

        var player = World.Player;
        if (!ProgressionService.TrySelectPerk(player, Content, perkId, out message))
        {
            return false;
        }

        Bus?.EmitLogMessage(message);
        Bus?.EmitHPChanged(player.Id, player.Stats.HP, player.Stats.MaxHP);
        Bus?.EmitProgressionChanged(player.Id);
        return true;
    }

    public int ResolveMerchantBuyPrice(int basePrice)
    {
        return World?.Player is null ? basePrice : ResolveMerchantBuyPrice(World.Player, basePrice);
    }

    public bool TryBuyMerchantOffer(EntityId merchantId, int offerIndex, out string message)
    {
        message = string.Empty;
        if (!TryResolveMerchantInteraction(merchantId, out var player, out var inventory, out var wallet, out var merchant, out var merchantComponent, out message))
        {
            return false;
        }

        if (Content is null)
        {
            message = "Content database is unavailable.";
            return false;
        }

        if (offerIndex < 0 || offerIndex >= merchantComponent.Offers.Count)
        {
            message = "That item is no longer available.";
            return false;
        }

        var offer = merchantComponent.Offers[offerIndex];
        if (offer.Quantity <= 0)
        {
            message = "That item is sold out.";
            return false;
        }

        if (!Content.TryGetItemTemplate(offer.ItemTemplateId, out var template))
        {
            message = $"The merchant's stock references unknown item '{offer.ItemTemplateId}'.";
            return false;
        }

        var price = ResolveMerchantBuyPrice(player, offer.Price);

        if (wallet.Gold < price)
        {
            message = $"You need {price} gold for {template.DisplayName}.";
            return false;
        }

        var purchasedItem = new ItemInstance
        {
            InstanceId = EntityId.New(),
            TemplateId = offer.ItemTemplateId,
            StackCount = 1,
            IsIdentified = true,
        };

        if (!inventory.CanAccept(purchasedItem, template.MaxStack))
        {
            message = "Your pack is too full to buy that.";
            return false;
        }

        wallet.Gold -= price;
        offer.Quantity--;
        if (template.MaxStack > 1)
        {
            inventory.AddWithStacking(purchasedItem, template.MaxStack);
        }
        else
        {
            inventory.Add(purchasedItem);
        }

        Bus?.EmitInventoryChanged(player.Id);
        Bus?.EmitCurrencyChanged(player.Id, wallet.Gold);
        message = $"Bought {template.DisplayName} from {merchant.Name} for {price} gold.";
        Bus?.EmitLogMessage(message);
        return true;
    }

    public bool TrySellItemToMerchant(EntityId merchantId, EntityId itemInstanceId, out string message)
    {
        message = string.Empty;
        if (!TryResolveMerchantInteraction(merchantId, out var player, out var inventory, out var wallet, out _, out _, out message))
        {
            return false;
        }

        if (Content is null)
        {
            message = "Content database is unavailable.";
            return false;
        }

        var item = inventory.Get(itemInstanceId);
        if (item is null)
        {
            message = "You are not carrying that item.";
            return false;
        }

        if (!Content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            message = $"Unknown item '{item.TemplateId}'.";
            return false;
        }

        var equippedSlot = inventory.GetEquippedSlot(item.InstanceId);
        if (equippedSlot != EquipSlot.None && inventory.TryUnequip(equippedSlot, out var removed) && removed is not null)
        {
            ApplyStatModifiers(player.Stats, removed.StatModifiers, -1);
            Bus?.EmitEquipmentChanged(player.Id, equippedSlot, null);
        }

        var sellQuantity = item.StackCount > 1 ? 1 : int.MaxValue;
        if (!inventory.RemoveQuantity(item.InstanceId, sellQuantity, out var soldItem) || soldItem is null)
        {
            message = "Selling failed because the item could not be removed.";
            return false;
        }

        var goldEarned = ResolveSellPrice(template) * Math.Max(1, soldItem.StackCount);
        wallet.Gold += goldEarned;

        Bus?.EmitInventoryChanged(player.Id);
        Bus?.EmitCurrencyChanged(player.Id, wallet.Gold);
        message = $"Sold {template.DisplayName} for {goldEarned} gold.";
        Bus?.EmitLogMessage(message);
        return true;
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
        var entityPositionsBefore = SnapshotEntityPositions(World);
        var playerId = World.Player.Id;
        var inventoryBefore = SnapshotInventory(actorBefore?.GetComponent<InventoryComponent>());
        var equipmentBefore = SnapshotEquipment(actorBefore?.GetComponent<InventoryComponent>());
        var progressionBefore = SnapshotProgression(actorBefore);

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

        if (outcome.Result == ActionResult.Success
            && action.ActorId == playerId
            && action.Type is not ActionType.Descend and not ActionType.Ascend)
        {
            ProcessEnemyResponses(playerId, outcome);
        }

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
            EmitStateDelta(action, actorPositionBefore, entityPositionsBefore, inventoryBefore, equipmentBefore, outcome.DirtyPositions, playerId);
            EmitProgressionChanges(playerId, progressionBefore);
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
        BindBus(ResolveEventBus());
        if (Content is not null)
        {
            ResolveContentAutoload()?.SetDatabase(Content);
        }

        if (Generator is not null && Scheduler is not null && Content is not null && SaveManager is not null)
        {
            return;
        }

        try
        {
            var content = Content as ContentLoader ?? ContentLoader.LoadFromDirectory(ToolPaths.ResolveContentDirectory());
            ResolveContentAutoload()?.SetDatabase(content);

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

    private EventBus? ResolveEventBus()
    {
        return Bus
            ?? GetNodeOrNull<EventBus>("/root/EventBus")
            ?? AutoloadResolver.Resolve<EventBus>(this, "EventBus");
    }

    private ContentDatabase? ResolveContentAutoload()
    {
        return GetNodeOrNull<ContentDatabase>("/root/ContentDatabase")
            ?? AutoloadResolver.Resolve<ContentDatabase>(this, "ContentDatabase");
    }

    private (WorldState World, FloorEntrances Entrances) CreateGeneratedWorld(int seed, int depth)
    {
        if (Generator is null || Content is null)
        {
            throw new InvalidOperationException("Generator and content services must be initialized before creating a world.");
        }

        var world = new WorldState();
        world.ContentDatabase = Content;
        var level = Generator.GenerateLevel(world, seed, depth);
        world.ContentDatabase = Content;
        var previousTurnNumber = World?.TurnNumber ?? 0;
        var rng = new Random(MixSeed(seed, depth));

        world.TurnNumber = previousTurnNumber;
        PopulateWorld(world, level, rng);
        return (world, new FloorEntrances(level.PlayerSpawn, level.StairsDown));
    }

    private void PopulateWorld(WorldState world, LevelData level, Random rng)
    {
        if (Content is null)
        {
            throw new InvalidOperationException("Content service is not available.");
        }

        var enemies = Content.GetAvailableEnemies(world.Depth);
        var enemySpawns = level.EnemySpawnDetails
            ?? level.EnemySpawns.Select(position => new EnemySpawnData(position)).ToArray();
        foreach (var spawn in enemySpawns)
        {
            var template = ResolveEnemyTemplate(enemies, spawn, rng, world.Depth);
            if (template is null)
            {
                continue;
            }

            if (!TryResolveSpawnPosition(world, spawn.Position, requireEmptyTile: true, avoidStairs: true, out var enemyPosition))
            {
                continue;
            }

            world.AddEntity(CreateEnemyEntity(template, enemyPosition, rng));
        }

        var itemSpawns = level.ItemSpawnDetails
            ?? level.ItemSpawns.Select(position => new ItemSpawnData(position)).ToArray();
        foreach (var spawn in itemSpawns)
        {
            if (!TryResolveSpawnPosition(world, spawn.Position, requireEmptyTile: false, avoidStairs: true, out var itemPosition))
            {
                continue;
            }

            foreach (var item in CreateFloorItems(world.Depth, rng, spawn.QualityBonus, spawn.TemplateId))
            {
                world.DropItem(itemPosition, item);
            }
        }

        var chestSpawns = level.ChestSpawnDetails
            ?? (level.ChestSpawns ?? Array.Empty<Position>()).Select(position => new ChestSpawnData(position)).ToArray();
        foreach (var spawn in chestSpawns)
        {
            if (!TryResolveSpawnPosition(world, spawn.Position, requireEmptyTile: true, avoidStairs: true, out var chestPosition))
            {
                continue;
            }

            world.AddEntity(CreateChestEntity(chestPosition, world.Depth, rng, spawn.LootTableId));
        }

        SpawnAuthoredNpcs(world, level.NpcSpawns ?? Array.Empty<NpcSpawnData>());
        SpawnNpcs(world, rng);
    }

    private static void AttachAbilities(Entity enemy, EnemyTemplate template, IContentDatabase content)
    {
        if (content is not ContentLoader loader)
        {
            return;
        }

        if (!loader.EnemyDefinitions.TryGetValue(template.TemplateId, out var definition))
        {
            return;
        }

        if (definition.Abilities.Count == 0)
        {
            return;
        }

        var abilitiesComp = new AbilitiesComponent();
        foreach (var abilityRef in definition.Abilities)
        {
            abilitiesComp.Slots.Add(new EnemyAbilitySlot
            {
                AbilityId = abilityRef.AbilityId,
                Cooldown = abilityRef.Cooldown,
                Priority = abilityRef.Priority,
            });
        }

        enemy.SetComponent(abilitiesComp);
        enemy.SetComponent(new CooldownComponent());
    }

    private Entity CreateEnemyEntity(EnemyTemplate template, Position spawn, Random rng)
    {
        var enemy = new Entity(
            template.DisplayName,
            spawn,
            template.BaseStats.Clone(),
            template.Faction,
            id: EntityId.NewSeeded(rng));
        enemy.SetComponent<IBrain>(BrainFactory.Create(template));
        enemy.SetComponent(new XpValueComponent { Value = template.XpValue });
        AttachAbilities(enemy, template, Content!);
        return enemy;
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
        player.SetComponent(new WalletComponent { Gold = StartingGold });
        player.SetComponent(new ProgressionComponent());
        player.SetComponent(new IdentityComponent
        {
            RaceId = character.RaceId,
            GenderId = character.GenderId,
            AppearanceId = character.AppearanceId,
            SpriteVariantId = PlayerVisualCatalog.ComposeVariantId(
                character.RaceId,
                character.GenderId,
                character.AppearanceId,
                character.Archetype),
        });
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

    private IReadOnlyList<ItemInstance> CreateFloorItems(int depth, Random rng, int qualityBonus = 0, string? fixedTemplateId = null)
    {
        var content = Content ?? throw new InvalidOperationException("Content service is not available.");
        var effectiveDepth = Math.Max(0, depth + Math.Max(0, qualityBonus));

        if (!string.IsNullOrWhiteSpace(fixedTemplateId))
        {
            return content.TryGetItemTemplate(fixedTemplateId, out _)
                ? new[]
                {
                    new ItemInstance
                    {
                        InstanceId = EntityId.NewSeeded(rng),
                        TemplateId = fixedTemplateId,
                        StackCount = 1,
                        IsIdentified = false,
                    },
                }
                : Array.Empty<ItemInstance>();
        }

        if (content is ContentLoader loader)
        {
            try
            {
                var rolls = LootTableResolver.RollTable(loader, ResolveFloorLootTableId(effectiveDepth), rng, effectiveDepth);
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

        var templates = content.GetAvailableItems(effectiveDepth);
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

    private EnemyTemplate? ResolveEnemyTemplate(IReadOnlyList<EnemyTemplate> templates, EnemySpawnData spawn, Random rng, int depth)
    {
        if (Content is not null && !string.IsNullOrWhiteSpace(spawn.TemplateId) && Content.TryGetEnemyTemplate(spawn.TemplateId, out var fixedTemplate))
        {
            return fixedTemplate;
        }

        var candidates = spawn.IsBoss && Content is not null
            ? Content.GetAvailableEnemies(depth + 2)
            : templates;
        if (candidates.Count == 0)
        {
            candidates = templates;
        }

        return candidates.Count == 0 ? null : SelectEnemyTemplate(candidates, rng);
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

        var currentWorld = World;
        var player = currentWorld.Player;
        var previousPosition = player.Position;

        try
        {
            var previousFloor = CurrentFloor;
            var targetWorld = _cachedFloors.TryGetValue(targetFloor, out var cachedFloor)
                ? cachedFloor
                : null;

            if (targetWorld is null)
            {
                var generatedFloor = CreateGeneratedWorld(Seed, targetFloor);
                targetWorld = generatedFloor.World;
                _cachedFloors[targetFloor] = targetWorld;
                _floorEntrances[targetFloor] = generatedFloor.Entrances;
            }

            if (targetWorld.GetEntity(player.Id) is not null)
            {
                targetWorld.RemoveEntity(player.Id);
            }

            var arrivalTile = targetFloor > previousFloor ? TileType.StairsUp : TileType.StairsDown;
            var entrances = ResolveFloorEntrances(targetFloor, targetWorld);
            var arrivalPosition = arrivalTile == TileType.StairsUp ? entrances.StairsUp : entrances.StairsDown;
            PlacePlayerInWorld(targetWorld, player, arrivalPosition, arrivalTile);

            currentWorld.RemoveEntity(player.Id);
            _cachedFloors[previousFloor] = currentWorld;
            _cachedFloors[targetFloor] = targetWorld;
            Scheduler = new TurnScheduler();
            LoadWorld(targetWorld);
            Bus?.EmitLevelTransition(previousFloor, targetFloor);
            Bus?.EmitLogMessage($"Travelled to floor {targetFloor}.");
            return true;
        }
        catch (Exception ex)
        {
            player.Position = previousPosition;
            currentWorld.Player = player;
            if (currentWorld.GetEntity(player.Id) is null)
            {
                currentWorld.AddEntity(player);
            }

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

    private static void PlacePlayerInWorld(WorldState world, IEntity player, Position arrivalPosition, TileType arrivalTile)
    {
        if (world.GetEntity(player.Id) is not null)
        {
            world.RemoveEntity(player.Id);
        }

        EnsureArrivalTile(world, arrivalPosition, arrivalTile);
        player.Stats.Energy = 0;
        world.Player = player;

        var primaryPosition = ResolveArrivalPosition(world, arrivalPosition, arrivalTile);
        if (TryPlacePlayerAt(world, player, primaryPosition, arrivalPosition, arrivalTile))
        {
            return;
        }

        foreach (var candidate in EnumerateArrivalFallbacks(world, arrivalPosition))
        {
            if (TryPlacePlayerAt(world, player, candidate, arrivalPosition, arrivalTile))
            {
                return;
            }
        }

        throw new InvalidOperationException($"No viable tile exists to place the player near arrival tile {arrivalPosition}.");
    }

    private static Position ResolveArrivalPosition(WorldState world, Position arrival, TileType arrivalTile)
    {
        if (arrival == Position.Invalid)
        {
            arrival = FindArrivalPosition(world, arrivalTile);
        }

        if (IsArrivalCandidateAvailable(world, arrival))
        {
            return arrival;
        }

        var frontier = new Queue<Position>();
        var visited = new HashSet<Position> { arrival };
        frontier.Enqueue(arrival);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var delta in Position.AllDirections)
            {
                var candidate = current + delta;
                if (!world.InBounds(candidate) || !visited.Add(candidate))
                {
                    continue;
                }

                if (IsArrivalCandidateAvailable(world, candidate))
                {
                    return candidate;
                }

                frontier.Enqueue(candidate);
            }
        }

        throw new InvalidOperationException($"No free walkable tile exists near arrival tile {arrival}.");
    }

    private static bool IsArrivalCandidateAvailable(WorldState world, Position position)
    {
        return world.IsWalkable(position) && world.GetEntityAt(position) is null;
    }

    private static void EnsureArrivalTile(WorldState world, Position arrivalPosition, TileType arrivalTile)
    {
        if (arrivalPosition == Position.Invalid)
        {
            return;
        }

        if (!world.InBounds(arrivalPosition))
        {
            throw new InvalidOperationException($"Arrival tile {arrivalPosition} is out of bounds.");
        }

        if (world.GetTile(arrivalPosition) != arrivalTile)
        {
            world.SetTile(arrivalPosition, arrivalTile);
        }
    }

    private static bool TryPlacePlayerAt(WorldState world, IEntity player, Position candidate, Position arrivalPosition, TileType arrivalTile)
    {
        if (!world.InBounds(candidate) || world.GetEntityAt(candidate) is not null)
        {
            return false;
        }

        if (!world.IsWalkable(candidate))
        {
            world.SetTile(candidate, candidate == arrivalPosition ? arrivalTile : TileType.Floor);
        }

        player.Position = candidate;
        try
        {
            world.AddEntity(player);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IEnumerable<Position> EnumerateArrivalFallbacks(WorldState world, Position arrivalPosition)
    {
        var frontier = new Queue<Position>();
        var visited = new HashSet<Position> { arrivalPosition };
        frontier.Enqueue(arrivalPosition);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var delta in Position.AllDirections)
            {
                var candidate = current + delta;
                if (!world.InBounds(candidate) || !visited.Add(candidate))
                {
                    continue;
                }

                yield return candidate;
                frontier.Enqueue(candidate);
            }
        }
    }

    private FloorEntrances ResolveFloorEntrances(int floor, WorldState world)
    {
        if (_floorEntrances.TryGetValue(floor, out var entrances))
        {
            return entrances;
        }

        entrances = DiscoverFloorEntrances(world);
        _floorEntrances[floor] = entrances;
        return entrances;
    }

    private void RememberFloorEntrances(WorldState world)
    {
        _floorEntrances[world.Depth] = DiscoverFloorEntrances(world);
    }

    private static FloorEntrances DiscoverFloorEntrances(WorldState world)
    {
        return new FloorEntrances(
            FindArrivalPositionOrInvalid(world, TileType.StairsUp),
            FindArrivalPositionOrInvalid(world, TileType.StairsDown));
    }

    private static Position FindArrivalPositionOrInvalid(IWorldState world, TileType tileType)
    {
        try
        {
            return FindArrivalPosition(world, tileType);
        }
        catch
        {
            return Position.Invalid;
        }
    }

    private void ProcessEnemyResponses(EntityId playerId, ActionOutcome aggregateOutcome)
    {
        if (World is null || Scheduler is null)
        {
            return;
        }

        while (World.GetEntity(playerId) is { IsAlive: true } player
            && player.Stats.Energy < Scheduler.EnergyThreshold
            && HasLivingNonPlayerActors(World, playerId))
        {
            var enemyOutcome = _gameLoop.ProcessRound(
                World,
                new NextActorScheduler(Scheduler, actor => actor.Id != playerId),
                actor =>
                {
                    Bus?.EmitEntityTurnStarted(actor.Id);
                    var brain = actor.GetComponent<IBrain>();
                    return brain?.DecideAction(actor, World, _pathfinder) ?? new WaitAction(actor.Id);
                });

            if (enemyOutcome.CombatEvents.Count == 0
                && enemyOutcome.LogMessages.Count == 0
                && enemyOutcome.DirtyPositions.Count == 0)
            {
                break;
            }

            aggregateOutcome.CombatEvents.AddRange(enemyOutcome.CombatEvents);
            aggregateOutcome.LogMessages.AddRange(enemyOutcome.LogMessages);
            aggregateOutcome.DirtyPositions.AddRange(enemyOutcome.DirtyPositions);
        }
    }

    private void RegisterWorldEntities(WorldState world)
    {
        if (Scheduler is null)
        {
            return;
        }

        foreach (var entity in world.Entities)
        {
            if (ShouldScheduleEntity(entity))
            {
                Scheduler.Register(entity);
            }
        }
    }

    private static bool HasLivingNonPlayerActors(WorldState world, EntityId playerId)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.Id != playerId && entity.IsAlive && ShouldScheduleEntity(entity))
            {
                return true;
            }
        }

        return false;
    }

    private void EmitWorldSnapshot(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            Bus?.EmitEntitySpawned(entity);
            Bus?.EmitHPChanged(entity.Id, entity.Stats.HP, entity.Stats.MaxHP);
        }

        Bus?.EmitInventoryChanged(world.Player.Id);
        if (world.Player.GetComponent<WalletComponent>() is { } wallet)
        {
            Bus?.EmitCurrencyChanged(world.Player.Id, wallet.Gold);
        }
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
        Dictionary<EntityId, Position> entityPositionsBefore,
        HashSet<EntityId> inventoryBefore,
        Dictionary<EquipSlot, EntityId> equipmentBefore,
        IReadOnlyList<Position> dirtyPositions,
        EntityId playerId)
    {
        if (World is null)
        {
            return;
        }

        foreach (var pair in entityPositionsBefore)
        {
            var entityAfterMove = World.GetEntity(pair.Key);
            if (entityAfterMove is not null && pair.Value != entityAfterMove.Position)
            {
                Bus?.EmitEntityMoved(pair.Key, pair.Value, entityAfterMove.Position);
            }
        }

        var actorAfter = World.GetEntity(action.ActorId);
        if (actorAfter is not null)
        {
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
                    || action.Type is ActionType.UseItem or ActionType.ToggleEquip or ActionType.DropItem or ActionType.PickupItem or ActionType.OpenChest)
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

        if (Bus is null || dirtyPositions.Count == 0)
        {
            return;
        }

        var uniqueDirtyPositions = new HashSet<Position>(dirtyPositions);
        foreach (var position in uniqueDirtyPositions)
        {
            if (World.InBounds(position))
            {
                Bus.EmitTileChanged(position);
            }
        }
    }

    private static Dictionary<EntityId, Position> SnapshotEntityPositions(WorldState world)
    {
        var snapshot = new Dictionary<EntityId, Position>(world.Entities.Count);
        foreach (var entity in world.Entities)
        {
            snapshot[entity.Id] = entity.Position;
        }

        return snapshot;
    }

    private static (int Experience, int Level, int UnspentStatPoints, int UnspentPerkChoices) SnapshotProgression(IEntity? entity)
    {
        var progression = entity?.GetComponent<ProgressionComponent>();
        return progression is null
            ? (0, 0, 0, 0)
            : (progression.Experience, progression.Level, progression.UnspentStatPoints, progression.UnspentPerkChoices);
    }

    private void EmitProgressionChanges(EntityId playerId, (int Experience, int Level, int UnspentStatPoints, int UnspentPerkChoices) before)
    {
        if (World is null || Bus is null)
        {
            return;
        }

        var player = World.GetEntity(playerId);
        var progression = player?.GetComponent<ProgressionComponent>();
        if (progression is null)
        {
            return;
        }

        if (progression.Experience != before.Experience)
        {
            Bus.EmitExperienceGained(playerId, progression.Experience - before.Experience, progression.Experience);
        }

        if (progression.Level != before.Level)
        {
            for (var level = before.Level + 1; level <= progression.Level; level++)
            {
                Bus.EmitLeveledUp(playerId, level);
            }
        }

        if (progression.Experience != before.Experience
            || progression.Level != before.Level
            || progression.UnspentStatPoints != before.UnspentStatPoints
            || progression.UnspentPerkChoices != before.UnspentPerkChoices)
        {
            Bus.EmitProgressionChanged(playerId);
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

    private static bool ShouldScheduleEntity(IEntity entity)
    {
        return entity.Faction != Faction.Neutral || entity.GetComponent<IBrain>() is not null;
    }

    private Entity CreateChestEntity(Position spawn, int depth, Random rng, string? lootTableId = null)
    {
        var chest = new Entity(
            "Treasure Chest",
            spawn,
            new Stats
            {
                HP = 1,
                MaxHP = 1,
                Attack = 0,
                Accuracy = 0,
                Defense = 0,
                Evasion = 0,
                Speed = 0,
                ViewRadius = 0,
            },
            Faction.Neutral,
            id: EntityId.NewSeeded(rng));

        chest.SetComponent(new ChestComponent
        {
            LootTableId = ResolveChestLootTableId(depth, lootTableId),
        });

        return chest;
    }

    private string ResolveChestLootTableId(int depth, string? explicitLootTableId = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitLootTableId)
            && (Content is not ContentLoader explicitLoader || explicitLoader.LootTables.ContainsKey(explicitLootTableId)))
        {
            return explicitLootTableId;
        }

        return ResolveFloorLootTableId(depth);
    }

    private string ResolveFloorLootTableId(int depth)
    {
        if (Content is ContentLoader loader && depth >= 4 && loader.LootTables.ContainsKey("deep_floor_loot"))
        {
            return "deep_floor_loot";
        }

        return "floor_loot";
    }

    private void SpawnAuthoredNpcs(WorldState world, IReadOnlyList<NpcSpawnData> npcSpawns)
    {
        if (Content is null || npcSpawns.Count == 0)
        {
            return;
        }

        foreach (var spawn in npcSpawns)
        {
            if (!Content.TryGetNpcTemplate(spawn.TemplateId, out var template))
            {
                continue;
            }

            if (!TryResolveSpawnPosition(world, spawn.Position, requireEmptyTile: true, avoidStairs: true, out var npcPosition))
            {
                continue;
            }

            world.AddEntity(CreateNpcEntity(template, npcPosition));
        }
    }

    private void SpawnNpcs(WorldState world, Random rng)
    {
        if (Content is null)
        {
            return;
        }

        var availableNpcs = Content.GetAvailableNpcs(world.Depth);
        if (availableNpcs.Count == 0)
        {
            return;
        }

        var existingTemplates = world.Entities
            .Select(entity => entity.GetComponent<NpcComponent>()?.TemplateId)
            .Where(templateId => !string.IsNullOrWhiteSpace(templateId))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var remainingSlots = Math.Max(0, 2 - existingTemplates.Count);
        if (remainingSlots == 0)
        {
            return;
        }

        var spawned = 0;
        foreach (var npcTemplate in availableNpcs.OrderByDescending(template => template.IsMerchant).ThenBy(template => template.TemplateId, StringComparer.Ordinal))
        {
            if (spawned >= remainingSlots)
            {
                break;
            }

            if (existingTemplates.Contains(npcTemplate.TemplateId))
            {
                continue;
            }

            if (!TryFindNpcSpawn(world, rng, out var spawn))
            {
                break;
            }

            world.AddEntity(CreateNpcEntity(npcTemplate, spawn));
            existingTemplates.Add(npcTemplate.TemplateId);
            spawned++;
        }
    }

    private static bool TryFindNpcSpawn(WorldState world, Random rng, out Position spawn)
    {
        var candidates = FindNpcSpawnCandidates(world, preferOpenArea: true).ToList();

        if (candidates.Count == 0)
        {
            candidates = FindNpcSpawnCandidates(world, preferOpenArea: false).ToList();
        }

        if (candidates.Count == 0)
        {
            spawn = Position.Invalid;
            return false;
        }

        spawn = candidates[rng.Next(candidates.Count)];
        return true;
    }

    private static bool TryResolveSpawnPosition(WorldState world, Position requested, bool requireEmptyTile, bool avoidStairs, out Position resolved)
    {
        if (IsValidSpawnPosition(world, requested, requireEmptyTile, avoidStairs))
        {
            resolved = requested;
            return true;
        }

        var frontier = new Queue<Position>();
        var visited = new HashSet<Position>();

        if (world.InBounds(requested))
        {
            frontier.Enqueue(requested);
            visited.Add(requested);
        }
        else
        {
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    var candidate = new Position(x, y);
                    if (IsValidSpawnPosition(world, candidate, requireEmptyTile, avoidStairs))
                    {
                        resolved = candidate;
                        return true;
                    }
                }
            }

            resolved = Position.Invalid;
            return false;
        }

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var delta in Position.AllDirections)
            {
                var candidate = current + delta;
                if (!world.InBounds(candidate) || !visited.Add(candidate))
                {
                    continue;
                }

                if (IsValidSpawnPosition(world, candidate, requireEmptyTile, avoidStairs))
                {
                    resolved = candidate;
                    return true;
                }

                frontier.Enqueue(candidate);
            }
        }

        resolved = Position.Invalid;
        return false;
    }

    private static bool IsValidSpawnPosition(WorldState world, Position position, bool requireEmptyTile, bool avoidStairs)
    {
        if (!world.InBounds(position) || !world.IsWalkable(position))
        {
            return false;
        }

        if (avoidStairs && world.GetTile(position) is TileType.StairsUp or TileType.StairsDown)
        {
            return false;
        }

        return !requireEmptyTile || world.GetEntityAt(position) is null;
    }

    private static IEnumerable<Position> FindNpcSpawnCandidates(WorldState world, bool preferOpenArea)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (!IsEligibleNpcSpawnTile(world, position))
                {
                    continue;
                }

                if (preferOpenArea)
                {
                    if (CountWalkableNeighbors(world, position) < 3)
                    {
                        continue;
                    }

                    if (IsAdjacentToDoor(world, position))
                    {
                        continue;
                    }
                }

                yield return position;
            }
        }
    }

    private static bool IsEligibleNpcSpawnTile(WorldState world, Position position)
    {
        if (!world.IsWalkable(position)
            || world.GetEntityAt(position) is not null
            || world.GetTile(position) is TileType.Door or TileType.StairsUp or TileType.StairsDown)
        {
            return false;
        }

        return !IsNearStairs(world, position, 4);
    }

    private static int CountWalkableNeighbors(WorldState world, Position position)
    {
        var count = 0;
        foreach (var delta in Position.Cardinals)
        {
            var candidate = position + delta;
            if (world.InBounds(candidate) && world.IsWalkable(candidate) && world.GetTile(candidate) != TileType.Door)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsAdjacentToDoor(WorldState world, Position position)
    {
        foreach (var delta in Position.Cardinals)
        {
            var candidate = position + delta;
            if (world.InBounds(candidate) && world.GetTile(candidate) == TileType.Door)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNearStairs(WorldState world, Position position, int maxDistance)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var candidate = new Position(x, y);
                if (world.GetTile(candidate) is TileType.StairsUp or TileType.StairsDown
                    && position.DistanceTo(candidate) <= maxDistance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Entity CreateNpcEntity(NpcTemplate template, Position spawn)
    {
        var npc = new Entity(
            template.DisplayName,
            spawn,
            new Stats
            {
                HP = 1,
                MaxHP = 1,
                Attack = 0,
                Accuracy = 0,
                Defense = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 6,
            },
            Faction.Neutral,
            id: EntityId.New());

        npc.SetComponent(new NpcComponent
        {
            TemplateId = template.TemplateId,
            Role = template.Role,
            DialogueId = template.DialogueId,
        });
        npc.SetComponent(new IdentityComponent
        {
            RaceId = template.RaceId,
            GenderId = template.GenderId,
            AppearanceId = template.AppearanceId,
            SpriteVariantId = PlayerVisualCatalog.ComposeVariantId(
                template.RaceId,
                template.GenderId,
                template.AppearanceId,
                template.ArchetypeId),
        });

        if (template.IsMerchant && template.MerchantOffers is not null)
        {
            npc.SetComponent(new MerchantComponent(template.MerchantOffers.Select(offer => new MerchantOfferState
            {
                ItemTemplateId = offer.ItemTemplateId,
                Price = offer.Price,
                Quantity = offer.Quantity,
            })));
        }

        return npc;
    }

    private IEntity? FindAdjacentNpc(WorldState world)
    {
        var player = world.Player;
        foreach (var delta in Position.Cardinals)
        {
            var candidate = world.GetEntityAt(player.Position + delta);
            if (candidate?.GetComponent<NpcComponent>() is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool TryResolveMerchantInteraction(
        EntityId merchantId,
        out IEntity player,
        out InventoryComponent inventory,
        out WalletComponent wallet,
        out IEntity merchant,
        out MerchantComponent merchantComponent,
        out string message)
    {
        player = null!;
        inventory = null!;
        wallet = null!;
        merchant = null!;
        merchantComponent = null!;
        message = string.Empty;

        if (World?.Player is null)
        {
            message = "No active run is available.";
            return false;
        }

        player = World.Player;
        inventory = player.GetComponent<InventoryComponent>()!;
        wallet = player.GetComponent<WalletComponent>()!;

        if (inventory is null || wallet is null)
        {
            message = "The player is missing inventory or wallet state.";
            return false;
        }

        merchant = World.GetEntity(merchantId)!;
        if (merchant is null)
        {
            message = "That merchant is no longer here.";
            return false;
        }

        merchantComponent = merchant.GetComponent<MerchantComponent>()!;
        if (merchantComponent is null)
        {
            message = $"{merchant.Name} has nothing to trade.";
            return false;
        }

        if (merchant.Position.ChebyshevTo(player.Position) > 1)
        {
            message = $"Move next to {merchant.Name} to trade.";
            return false;
        }

        return true;
    }

    private static int ResolveSellPrice(ItemTemplate template)
    {
        return Math.Max(1, template.Value / 2);
    }

    private int ResolveMerchantBuyPrice(IEntity player, int basePrice)
    {
        var discountPercent = ProgressionService.ResolveShopDiscountPercent(player, Content);
        var discounted = basePrice * Math.Max(0, 100 - discountPercent) / 100;
        return Math.Max(1, discounted);
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
