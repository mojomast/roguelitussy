using System;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class RelicHookTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Relic thorn wrap reflects damage to attacker", ThornWrapReflectsDamage);
        registry.Add("Simulation.Relic mirror shard reflection can kill the attacker", MirrorShardReflectionCanKill);
        registry.Add("Simulation.Relic lucky coin uses seeded combat rng", LuckyCoinUsesSeededRng);
        registry.Add("Simulation.Relic death mask buffs outgoing damage not incoming", DeathMaskBuffsOutgoingDamage);
        registry.Add("Simulation.Relic predator mark doubles only first hit per floor", PredatorMarkDoublesFirstHitPerFloor);
        registry.Add("Simulation.Relic iron skin shield absorbs incoming damage", IronSkinShieldAbsorbs);
        registry.Add("Simulation.Relic phoenix ash saves lethal hit once per run", PhoenixAshSavesLethalOnce);
        registry.Add("Simulation.Relic berserker heart triggers on low hp crossing", BerserkerHeartTriggersOnCrossing);
        registry.Add("Simulation.Relic void amulet cushions incoming damage", VoidAmuletCushionsDamage);
        registry.Add("Simulation.Relic time anchor clears tempo effects on floor enter", TimeAnchorClearsTempoEffects);
        registry.Add("Simulation.Relic cartographer lens reveals the floor", CartographerLensRevealsFloor);
        registry.Add("Simulation.Relic merchant badge discounts merchant offers once per floor", MerchantBadgeDiscountsOffers);
        registry.Add("Simulation.Relic cursed blade applies once and drains each round", CursedBladeAppliesOnceAndDrains);
        registry.Add("Simulation.Relic vampire fang kill heal is surfaced in death resolution", VampireFangKillHealIsSurfaced);
        registry.Add("Simulation.Relic attack action routes damage through relic hooks", AttackActionRoutesRelicHooks);
        registry.Add("Simulation.Relic kill milestones use resulting kill count", KillMilestonesUseResultingKillCount);
        registry.Add("Simulation.Relic glass cannon applies only once", GlassCannonAppliesOnlyOnce);
        registry.Add("Simulation.Relic warlord crest applies only missing floor bonus", WarlordCrestAppliesOnlyMissingBonus);
    }

    private static void ThornWrapReflectsDamage()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "thorn_wrap");
        var enemy = CreateEnemy(world, new Position(2, 1), hp: 10);

        var log = new System.Collections.Generic.List<string>();
        var dealt = RelicProcessor.ApplyIncomingDamage(world, player, enemy.Id, 5, log);

        Expect.Equal(5, dealt, "Thorn Wrap should not change the incoming damage");
        Expect.Equal(15, player.Stats.HP, "Player should take the full 5 damage");
        Expect.Equal(9, enemy.Stats.HP, "Attacker should take 1 reflected damage");
        Expect.True(log.Any(message => message.Contains("reflects", StringComparison.OrdinalIgnoreCase)), "Reflection should be logged");
    }

    private static void MirrorShardReflectionCanKill()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "mirror_shard");
        var enemy = CreateEnemy(world, new Position(2, 1), hp: 2);

        var log = new System.Collections.Generic.List<string>();
        RelicProcessor.ApplyIncomingDamage(world, player, enemy.Id, 3, log);

        Expect.True(world.GetEntity(enemy.Id) is null, "An attacker killed by reflection should be removed from the world");
    }

    private static void LuckyCoinUsesSeededRng()
    {
        var negateSeed = FindSeed(roll => roll < 10);
        var passSeed = FindSeed(roll => roll >= 10);

        var negateWorld = CreateWorld(negateSeed);
        var negatePlayer = CreatePlayerWithRelic(negateWorld, "lucky_coin");
        var negated = RelicProcessor.ApplyIncomingDamage(negateWorld, negatePlayer, null, 5);
        Expect.Equal(0, negated, "Lucky Coin should negate the hit when the seeded roll is under 10");
        Expect.Equal(20, negatePlayer.Stats.HP, "Negated damage should leave HP untouched");

        var passWorld = CreateWorld(passSeed);
        var passPlayer = CreatePlayerWithRelic(passWorld, "lucky_coin");
        var applied = RelicProcessor.ApplyIncomingDamage(passWorld, passPlayer, null, 5);
        Expect.Equal(5, applied, "Lucky Coin should not negate when the seeded roll is 10 or higher");
        Expect.Equal(15, passPlayer.Stats.HP, "Non-negated damage should apply in full");
    }

    private static void DeathMaskBuffsOutgoingDamage()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "death_mask");
        var enemy = CreateEnemy(world, new Position(2, 1), hp: 50);

        var dealt = RelicProcessor.ApplyIncomingDamage(world, player, enemy.Id, 5);
        Expect.Equal(5, dealt, "Death Mask must not amplify incoming damage");

        var buffed = RelicProcessor.ProcessOutgoingDamage(world, player, enemy, 10);
        Expect.Equal(12, buffed, "Death Mask should add 20% outgoing damage after taking a hit");

        world.TurnNumber = 20;
        var expired = RelicProcessor.ProcessOutgoingDamage(world, player, enemy, 10);
        Expect.Equal(10, expired, "Death Mask buff should expire after 5 turns");
    }

    private static void PredatorMarkDoublesFirstHitPerFloor()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "predator_mark");
        var enemy = CreateEnemy(world, new Position(2, 1), hp: 50);

        Expect.Equal(20, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, 10), "First hit on an enemy should be doubled");
        Expect.Equal(10, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, 10), "Second hit on the same enemy should be normal");

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(20, RelicProcessor.ProcessOutgoingDamage(world, player, enemy, 10), "Entering a new floor should reset first-hit tracking");
    }

    private static void IronSkinShieldAbsorbs()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "iron_skin");

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        var component = player.GetComponent<RelicComponent>()!;
        Expect.Equal(5, component.ShieldCharges, "Iron Skin should grant a 5 HP shield on floor enter");

        var dealt = RelicProcessor.ApplyIncomingDamage(world, player, null, 4);
        Expect.Equal(0, dealt, "Shield charges should absorb the whole hit");
        Expect.Equal(20, player.Stats.HP, "Absorbed damage should not reduce HP");
        Expect.Equal(1, component.ShieldCharges, "Absorption should consume shield charges");

        var partial = RelicProcessor.ApplyIncomingDamage(world, player, null, 3);
        Expect.Equal(2, partial, "Remaining damage should pass through once charges run out");
        Expect.Equal(18, player.Stats.HP, "Partial absorption should apply leftover damage");
    }

    private static void PhoenixAshSavesLethalOnce()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "phoenix_ash");
        player.Stats.HP = 3;

        var log = new System.Collections.Generic.List<string>();
        RelicProcessor.ApplyIncomingDamage(world, player, null, 10, log);
        Expect.Equal(1, player.Stats.HP, "Phoenix Ash should save a lethal hit at 1 HP");
        Expect.True(player.GetComponent<RelicComponent>()!.LowHpRelicFired, "Phoenix Ash should be marked as used");
        Expect.True(log.Any(message => message.Contains("Phoenix Ash", StringComparison.Ordinal)), "Phoenix save should be logged");

        RelicProcessor.ApplyIncomingDamage(world, player, null, 10);
        Expect.True(player.Stats.HP <= 0, "Phoenix Ash should fire only once per run");
    }

    private static void BerserkerHeartTriggersOnCrossing()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "berserker_heart");
        var baseAttack = player.Stats.Attack;

        RelicProcessor.ApplyIncomingDamage(world, player, null, 16);
        Expect.Equal(baseAttack + 4, player.Stats.Attack, "Crossing below 25% HP should trigger Berserker Heart");

        RelicProcessor.ApplyIncomingDamage(world, player, null, 1);
        Expect.Equal(baseAttack + 4, player.Stats.Attack, "Damage while already below the threshold should not re-trigger");
    }

    private static void VoidAmuletCushionsDamage()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "void_amulet");

        var dealt = RelicProcessor.ApplyIncomingDamage(world, player, null, 6);
        Expect.Equal(2, dealt, "Void Amulet should cushion incoming damage by its effect value (minimum 1 through)");
        Expect.Equal(18, player.Stats.HP, "Cushioned damage should be applied");
    }

    private static void TimeAnchorClearsTempoEffects()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "time_anchor");
        StatusEffectProcessor.ApplyEffect(player, StatusEffectType.Hasted, 5);

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());

        Expect.False(StatusEffectProcessor.HasEffect(player, StatusEffectType.Hasted), "Time Anchor should remove haste on floor enter");
    }

    private static void CartographerLensRevealsFloor()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "cartographer_lens");
        Expect.False(world.IsExplored(new Position(5, 5)), "Precondition: tile should start unexplored");

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());

        Expect.True(world.IsExplored(new Position(5, 5)), "Cartographer's Lens should mark the whole floor explored");
    }

    private static void MerchantBadgeDiscountsOffers()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "merchant_badge");
        var merchant = new StubEntity("Merchant", new Position(4, 4), Faction.Neutral);
        merchant.SetComponent(new MerchantComponent(new[] { new MerchantOfferState { ItemTemplateId = "sword_iron", Price = 100, Quantity = 1 } }));
        world.AddEntity(merchant);

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(85, merchant.GetComponent<MerchantComponent>()!.Offers[0].Price, "Merchant Badge should discount offers by 15%");

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(85, merchant.GetComponent<MerchantComponent>()!.Offers[0].Price, "Discount should not stack on the same floor");
    }

    private static void CursedBladeAppliesOnceAndDrains()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "cursed_blade");
        player.Stats.Energy = 1000;
        world.Player = player;
        var baseAttack = player.Stats.Attack;

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(baseAttack + 8, player.Stats.Attack, "Cursed Blade attack bonus should apply exactly once");

        var gameLoop = new GameLoop();
        var scheduler = new SinglePassScheduler(new[] { player.Id });
        var outcome = gameLoop.ProcessRound(world, scheduler, entity => new WaitAction(entity.Id));

        Expect.Equal(19, player.Stats.HP, "Cursed Blade should drain 1 HP at the end of the round");
        Expect.True(outcome.LogMessages.Any(message => message.Contains("Cursed Blade", StringComparison.Ordinal)), "The drain should be logged");
    }

    private static void VampireFangKillHealIsSurfaced()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "vampire_fang");
        player.Stats.HP = 5;
        var enemy = CreateEnemy(world, new Position(2, 1), hp: 1);

        var death = DeathResolver.ResolveKill(world, player, enemy);

        Expect.Equal(6, player.Stats.HP, "Vampire Fang should heal 1 HP on kill");
        Expect.NotNull(death.RelicMessages, "Kill resolution should carry relic proc messages");
        Expect.True(death.RelicMessages!.Any(message => message.Contains("Vampire Fang", StringComparison.Ordinal)), "The heal should be logged");

        var log = new System.Collections.Generic.List<string>();
        DeathResolver.AppendLootLogMessages(log, death);
        Expect.True(log.Any(message => message.Contains("Vampire Fang", StringComparison.Ordinal)), "Loot log append should surface relic messages");
    }

    private static void AttackActionRoutesRelicHooks()
    {
        // An enemy attacking a thorn-wrap player must reflect damage, whatever the to-hit roll
        // sequence is; scan seeds until the attack lands.
        for (var seed = 0; seed < 100; seed++)
        {
            var world = CreateWorld(seed);
            var player = CreatePlayerWithRelic(world, "thorn_wrap");
            var enemy = CreateEnemy(world, new Position(2, 1), hp: 10);

            var outcome = new AttackAction(enemy.Id, player.Id).Execute(world);
            Expect.Equal(ActionResult.Success, outcome.Result, "Attack should be valid");
            if (player.Stats.HP == 20)
            {
                continue; // missed; try another seed
            }

            Expect.Equal(9, enemy.Stats.HP, "AttackAction damage should dispatch on_damaged and reflect 1 damage");
            Expect.True(outcome.LogMessages.Any(message => message.Contains("Thorn Wrap", StringComparison.Ordinal)), "Relic proc should reach the action log");
            return;
        }

        Expect.True(false, "No seed produced a landed attack within 100 attempts");
    }

    private static void KillMilestonesUseResultingKillCount()
    {
        var boneWorld = CreateWorld();
        var bonePlayer = CreatePlayerWithRelic(boneWorld, "bone_amulet");
        bonePlayer.SetComponent(new ProgressionComponent { Kills = 2 });
        var boneDeath = DeathResolver.ResolveKill(boneWorld, bonePlayer, CreateEnemy(boneWorld, new Position(2, 1), hp: 1));

        Expect.Equal(3, bonePlayer.GetComponent<ProgressionComponent>()!.Kills, "The resolved kill should be visible to on_kill relics");
        Expect.Equal(25, bonePlayer.Stats.MaxHP, "Bone Amulet should trigger on the resulting third kill");
        Expect.Equal(25, bonePlayer.Stats.HP, "Bone Amulet should preserve missing HP when raising max HP");
        Expect.True(boneDeath.RelicMessages!.Any(message => message.Contains("Bone Amulet", StringComparison.Ordinal)), "Bone Amulet milestone should be surfaced");

        var fourthDeath = DeathResolver.ResolveKill(boneWorld, bonePlayer, CreateEnemy(boneWorld, new Position(3, 1), hp: 1));
        Expect.Equal(4, bonePlayer.GetComponent<ProgressionComponent>()!.Kills, "The next kill should advance normally");
        Expect.Equal(25, bonePlayer.Stats.MaxHP, "Bone Amulet should not repeat on kill four");
        Expect.False(fourthDeath.RelicMessages!.Any(message => message.Contains("Bone Amulet", StringComparison.Ordinal)), "Non-milestone kills should not log Bone Amulet");

        var soulWorld = CreateWorld();
        var soulPlayer = CreatePlayerWithRelic(soulWorld, "soul_collector");
        soulPlayer.SetComponent(new ProgressionComponent { Kills = 4 });
        var soulVictim = CreateEnemy(soulWorld, new Position(2, 1), hp: 1);
        var soulDeath = DeathResolver.ResolveKill(soulWorld, soulPlayer, soulVictim);

        Expect.Equal(5, soulPlayer.GetComponent<ProgressionComponent>()!.Kills, "Soul Collector should observe the resulting fifth kill");
        Expect.Equal(23, soulPlayer.Stats.MaxHP, "Soul Collector should grant its authored max HP on kill five");
        Expect.Equal(23, soulPlayer.Stats.HP, "Soul Collector should raise current HP with max HP");
        Expect.True(soulDeath.RelicMessages!.Any(message => message.Contains("Soul Collector", StringComparison.Ordinal)), "Soul Collector milestone should be surfaced");

        var duplicate = DeathResolver.ResolveKill(soulWorld, soulPlayer, soulVictim);
        Expect.Equal(0, duplicate.KillsAwarded, "Resolving an already removed victim must remain a no-op");
        Expect.Equal(5, soulPlayer.GetComponent<ProgressionComponent>()!.Kills, "Duplicate resolution must not repeat milestone progress");
    }

    private static void GlassCannonAppliesOnlyOnce()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "glass_cannon");
        player.Stats.Defense = 5;
        var first = new RelicHookContext();
        var second = new RelicHookContext();

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, first);
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, second);

        Expect.Equal(10, player.Stats.Attack, "Glass Cannon should double attack exactly once");
        Expect.Equal(2, player.Stats.Defense, "Glass Cannon should halve odd defense using integer division exactly once");
        Expect.True(player.GetComponent<RelicComponent>()!.AppliedOneTimeRelics.Contains("glass_cannon"), "Glass Cannon application should be persisted by the existing one-time state");
        Expect.True(first.LogMessages.Any(message => message.Contains("Glass Cannon", StringComparison.Ordinal)), "Initial Glass Cannon application should be logged");
        Expect.Equal(0, second.LogMessages.Count, "Repeated floor hooks should not log or reapply Glass Cannon");
    }

    private static void WarlordCrestAppliesOnlyMissingBonus()
    {
        var world = CreateWorld();
        var player = CreatePlayerWithRelic(world, "warlord_crest");
        var component = player.GetComponent<RelicComponent>()!;

        world.Depth = 2;
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(7, player.Stats.Attack, "Depth two should establish a cumulative +2 Warlord bonus");
        Expect.Equal(2, component.AppliedStatTotals["warlord_crest"], "Applied total should record the granted bonus");

        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(7, player.Stats.Attack, "Reentering the same floor must not duplicate Warlord bonus");

        world.Depth = 3;
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(9, player.Stats.Attack, "A deeper floor should add only the missing +2 bonus");
        Expect.Equal(4, component.AppliedStatTotals["warlord_crest"], "Applied total should advance to the depth target");

        world.Depth = 2;
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(9, player.Stats.Attack, "Ascending must not remove or duplicate the earned Warlord bonus");

        world.Depth = 6;
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        RelicProcessor.ProcessHook("on_floor_enter", player, world, world.ContentDatabase, new RelicHookContext());
        Expect.Equal(15, player.Stats.Attack, "Warlord bonus should cap at +10 without repeating at the cap");
        Expect.Equal(10, component.AppliedStatTotals["warlord_crest"], "Persisted total should honor the +10 cap");
    }

    private static int FindSeed(Func<int, bool> predicate)
    {
        for (var seed = 0; seed < 500; seed++)
        {
            if (predicate(new CombatResolver(seed).NextRandom(100)))
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed found matching the roll predicate.");
    }

    private static WorldState CreateWorld(int seed = 123)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = seed;
        world.ContentDatabase = new StubContentDatabase();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreatePlayerWithRelic(WorldState world, string relicId)
    {
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        Expect.True(RelicProcessor.AddRelic(player, world.ContentDatabase, relicId), $"Relic '{relicId}' should be addable");
        world.AddEntity(player);
        return player;
    }

    private static StubEntity CreateEnemy(WorldState world, Position position, int hp)
    {
        var enemy = new StubEntity("Enemy", position, Faction.Enemy,
            stats: new Stats { HP = hp, MaxHP = hp, Attack = 4, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        world.AddEntity(enemy);
        return enemy;
    }

    private sealed class SinglePassScheduler : ITurnScheduler
    {
        private readonly System.Collections.Generic.Queue<EntityId> _queue;
        private WorldState? _world;

        public SinglePassScheduler(System.Collections.Generic.IEnumerable<EntityId> order)
        {
            _queue = new System.Collections.Generic.Queue<EntityId>(order);
        }

        public int EnergyThreshold => 1000;

        public void BeginRound(WorldState world)
        {
            _world = world;
            world.TurnNumber++;
        }

        public bool HasNextActor() => _queue.Count > 0;

        public IEntity? GetNextActor() => _world is null || _queue.Count == 0 ? null : _world.GetEntity(_queue.Dequeue());

        public StatusTickResult? ConsumeEnergy(EntityId actorId, int cost)
        {
            if (_world?.GetEntity(actorId) is { } entity)
            {
                entity.Stats.Energy -= cost;
                return StatusEffectProcessor.Tick(_world, actorId);
            }

            return null;
        }

        public void EndRound(WorldState world)
        {
        }

        public void Register(IEntity entity)
        {
        }

        public void Unregister(EntityId id)
        {
        }

        public int GetOrder(EntityId actorId) => 0;

        public int NextOrder { get; set; }

        public void AttachWorld(WorldState world)
        {
        }
    }
}
