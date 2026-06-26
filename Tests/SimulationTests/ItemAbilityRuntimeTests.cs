using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class ItemAbilityRuntimeTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Items content health potion heals authored amount", ContentHealthPotionHealsAuthoredAmount);
        registry.Add("Simulation.Items content status potions apply authored durations", ContentStatusPotionsApplyAuthoredDurations);
        registry.Add("Simulation.Items self cast scroll resolves without target", SelfCastScrollResolvesWithoutTarget);
        registry.Add("Simulation.Items aimed scrolls require explicit targets", AimedScrollsRequireExplicitTargets);
        registry.Add("Simulation.Items targeted scroll succeeds with selected target", TargetedScrollSucceedsWithSelectedTarget);
        registry.Add("Simulation.Items UIActionFactory creates UseItemAction with target for fireball", UIActionFactoryCreatesUseItemActionWithTargetForFireball);
        registry.Add("Simulation.Items UIActionFactory creates UseItemAction with target for blink", UIActionFactoryCreatesUseItemActionWithTargetForBlink);
        registry.Add("Simulation.Items UseItemAction with invalid target fails validation", UseItemActionWithInvalidTargetFailsValidation);
        registry.Add("Simulation.Items UseItemAction out of range returns blocked", UseItemActionOutOfRangeReturnsBlocked);
        registry.Add("Simulation.Items content flags aimed scrolls as requiring target selection", ContentFlagsAimedScrollsAsRequiringTargetSelection);
        registry.Add("Simulation.Items content validation rejects mismatched cast ability target", ContentValidationRejectsMismatchedCastAbilityTarget);
    }

    private static void ContentHealthPotionHealsAuthoredAmount()
    {
        var content = LoadContent();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2), new Stats { HP = 10, MaxHP = 40, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var item = AddItem(actor, "potion_health");
        world.Player = actor;
        world.AddEntity(actor);

        Expect.True(content.TryGetItemTemplate("potion_health", out var template), "Health potion template should exist.");
        Expect.Equal(25, template.StatModifiers["heal"], "Health potion projection should preserve authored heal value.");

        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Health potion should execute successfully.");
        Expect.Equal(35, actor.Stats.HP, "Health potion should heal by the authored amount.");
    }

    private static void ContentStatusPotionsApplyAuthoredDurations()
    {
        var content = LoadContent();

        UseStatusPotion(content, "potion_haste", StatusEffectType.Hasted, expectedDuration: 10);
        UseStatusPotion(content, "potion_might", StatusEffectType.Empowered, expectedDuration: 8);
    }

    private static void SelfCastScrollResolvesWithoutTarget()
    {
        var content = LoadContent();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, "scroll_phase");
        world.Player = actor;
        world.AddEntity(actor);

        Expect.True(content.TryGetItemTemplate("scroll_phase", out var template), "Phase scroll template should exist.");
        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Self-targeted cast scroll should not require an explicit target.");
        var phased = StatusEffectProcessor.GetEffect(actor, StatusEffectType.Phased);
        Expect.True(phased is not null, "Phase scroll should apply the phased status.");
        Expect.Equal(2, phased!.RemainingTurns, "Phase scroll should use the authored ability duration.");
        Expect.False(actor.GetComponent<InventoryComponent>()!.Contains(item.InstanceId), "Successful scroll use should consume the item.");
    }

    private static void AimedScrollsRequireExplicitTargets()
    {
        var content = LoadContent();
        AssertAimedScrollRequiresTarget(content, "scroll_fireball");
        AssertAimedScrollRequiresTarget(content, "scroll_blink");
    }

    private static void TargetedScrollSucceedsWithSelectedTarget()
    {
        var content = LoadContent();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2), new Stats { HP = 30, MaxHP = 30, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var enemy = new StubEntity("Target", new Position(4, 2), Faction.Enemy, stats: new Stats { HP = 30, MaxHP = 30, Attack = 3, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        var item = AddItem(actor, "scroll_fireball");
        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(enemy);

        Expect.True(content.TryGetItemTemplate("scroll_fireball", out var template), "Fireball scroll template should exist.");
        var outcome = new UseItemAction(actor.Id, item.InstanceId, template, abilityTarget: enemy.Position).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Aimed scroll should succeed when a valid target position is supplied.");
        Expect.True(enemy.Stats.HP < enemy.Stats.MaxHP, "Fireball scroll should damage entities around the selected target.");
        Expect.False(actor.GetComponent<InventoryComponent>()!.Contains(item.InstanceId), "Successful aimed scroll use should consume the item.");
    }

    private static void UseStatusPotion(ContentLoader content, string templateId, StatusEffectType statusType, int expectedDuration)
    {
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, templateId);
        world.Player = actor;
        world.AddEntity(actor);

        Expect.True(content.TryGetItemTemplate(templateId, out var template), $"{templateId} template should exist.");
        Expect.Equal(expectedDuration, template.StatModifiers["duration"], $"{templateId} projection should preserve authored duration.");

        var outcome = new UseItemAction(actor.Id, item.InstanceId, template).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, $"{templateId} should execute successfully.");
        var applied = StatusEffectProcessor.GetEffect(actor, statusType);
        Expect.True(applied is not null, $"{templateId} should apply {statusType}.");
        Expect.Equal(expectedDuration, applied!.RemainingTurns, $"{templateId} should apply the authored duration.");
        Expect.Equal(1, applied.Magnitude, $"{templateId} should preserve default authored magnitude.");
        Expect.False(actor.GetComponent<InventoryComponent>()!.Contains(item.InstanceId), $"{templateId} should be consumed on success.");
    }

    private static void UIActionFactoryCreatesUseItemActionWithTargetForFireball()
    {
        var content = new StubContentDatabase();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, "scroll_fireball");
        world.Player = actor;
        world.AddEntity(actor);

        var target = new Position(4, 2);
        var action = UIActionFactory.CreateUseItemAction(world, content, actor.Id, item.InstanceId, target);

        Expect.NotNull(action, "UIActionFactory should create an action for an aimed scroll when a target is supplied.");
        Expect.True(action is UseItemAction, "The created action should be a UseItemAction.");
        var useItemAction = (UseItemAction)action!;
        Expect.True(useItemAction.HasAbilityTarget, "The action should carry an explicit ability target.");
        Expect.Equal(target, useItemAction.AbilityTarget, "The action target should match the supplied cursor position.");
    }

    private static void UIActionFactoryCreatesUseItemActionWithTargetForBlink()
    {
        var content = new StubContentDatabase();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, "scroll_blink");
        world.Player = actor;
        world.AddEntity(actor);

        var target = new Position(3, 3);
        var action = UIActionFactory.CreateUseItemAction(world, content, actor.Id, item.InstanceId, target);

        Expect.NotNull(action, "UIActionFactory should create an action for blink scroll when a target tile is supplied.");
        Expect.True(action is UseItemAction, "The created action should be a UseItemAction.");
        var useItemAction = (UseItemAction)action!;
        Expect.True(useItemAction.HasAbilityTarget, "The action should carry an explicit ability target.");
        Expect.Equal(target, useItemAction.AbilityTarget, "The action target should match the supplied tile position.");
    }

    private static void UseItemActionWithInvalidTargetFailsValidation()
    {
        var content = new StubContentDatabase();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, "scroll_blink");
        world.Player = actor;
        world.AddEntity(actor);

        world.SetTile(new Position(3, 3), TileType.Wall);
        var action = new UseItemAction(actor.Id, item.InstanceId, content.ItemTemplates["scroll_blink"], abilityTarget: new Position(3, 3));

        Expect.Equal(ActionResult.Blocked, action.Validate(world), "Blink scroll should be blocked when targeting an unwalkable tile.");
        var outcome = action.Execute(world);
        Expect.Equal(ActionResult.Blocked, outcome.Result, "Executing blink onto a wall should fail.");
        Expect.True(actor.GetComponent<InventoryComponent>()!.Contains(item.InstanceId), "Failed blink scroll use should not consume the item.");
    }

    private static void UseItemActionOutOfRangeReturnsBlocked()
    {
        var content = new StubContentDatabase();
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, "scroll_fireball");
        world.Player = actor;
        world.AddEntity(actor);

        var farTarget = new Position(12, 2);
        var action = new UseItemAction(actor.Id, item.InstanceId, content.ItemTemplates["scroll_fireball"], abilityTarget: farTarget);

        Expect.Equal(ActionResult.Blocked, action.Validate(world), "Fireball scroll should be blocked when the target is beyond ability range.");
        var outcome = action.Execute(world);
        Expect.Equal(ActionResult.Blocked, outcome.Result, "Executing fireball out of range should fail.");
        Expect.True(actor.GetComponent<InventoryComponent>()!.Contains(item.InstanceId), "Failed fireball scroll use should not consume the item.");
    }

    private static void ContentFlagsAimedScrollsAsRequiringTargetSelection()
    {
        var content = LoadContent();

        Expect.True(content.TryGetItemTemplate("scroll_fireball", out var fireball), "Fireball scroll template should exist.");
        Expect.True(fireball.RequiresTargetSelection, "Aimed fireball scroll should require field target selection.");

        Expect.True(content.TryGetItemTemplate("scroll_blink", out var blink), "Blink scroll template should exist.");
        Expect.True(blink.RequiresTargetSelection, "Aimed blink scroll should require field target selection.");

        Expect.True(content.TryGetItemTemplate("scroll_phase", out var phase), "Phase scroll template should exist.");
        Expect.False(phase.RequiresTargetSelection, "Self-targeted phase scroll should not require field target selection.");
    }

    private static void ContentValidationRejectsMismatchedCastAbilityTarget()
    {
        var items = new Dictionary<string, ItemDefinition>
        {
            ["bad_self_scroll"] = new()
            {
                Id = "bad_self_scroll",
                Name = "Bad Self Scroll",
                Description = "Claims self but references an aimed ability.",
                Type = "consumable",
                Slot = "none",
                Effects = new()
                {
                    new()
                    {
                        Type = "on_use",
                        Action = "cast_ability",
                        AbilityId = "fireball",
                        Target = "self",
                    },
                },
                Rarity = "common",
                Value = 1,
                Weight = 0.1,
                Stackable = true,
                MaxStack = 5,
                SpritePath = "res://Assets/Sprites/items/scroll_fireball.svg",
                SpriteAtlasCoords = new() { 0, 0 },
            },
        };

        var abilities = new Dictionary<string, AbilityDefinition>
        {
            ["fireball"] = new()
            {
                Id = "fireball",
                Name = "Fireball",
                Description = "Test ability.",
                Targeting = new AbilityTargetingDefinition
                {
                    Type = "aoe_circle",
                    Range = 8,
                    Radius = 3,
                    RequiresLos = true,
                    RequiresWalkable = false,
                },
                Costs = new AbilityCostDefinition { Energy = 1000 },
                Effects = new() { new() { Type = "damage", BaseValue = 10, DamageType = "fire" } },
                Animation = "none",
                Sfx = "none",
            },
        };

        var method = typeof(ContentLoader).GetMethod("ValidateItems", BindingFlags.NonPublic | BindingFlags.Static);
        var errors = new List<string>();
        method!.Invoke(null, new object[] { items, abilities, new Dictionary<string, StatusEffectDefinition>(), errors });

        Expect.True(errors.Count > 0, "Validation should reject a self-target item that references a non-self ability.");
        Expect.True(errors.Any(e => e.Contains("cast_ability:self") && e.Contains("targeting.type")), "Validation error should explain the self/targeting mismatch.");
    }

    private static void AssertAimedScrollRequiresTarget(ContentLoader content, string templateId)
    {
        var world = CreateWorld(content);
        var actor = CreateActor(new Position(2, 2));
        var item = AddItem(actor, templateId);
        var inventory = actor.GetComponent<InventoryComponent>()!;
        world.Player = actor;
        world.AddEntity(actor);

        Expect.True(content.TryGetItemTemplate(templateId, out var template), $"{templateId} template should exist.");
        var action = new UseItemAction(actor.Id, item.InstanceId, template);

        Expect.Equal(ActionResult.Invalid, action.Validate(world), $"{templateId} should fail validation without an explicit target.");
        var outcome = action.Execute(world);
        Expect.Equal(ActionResult.Invalid, outcome.Result, $"{templateId} should not execute without an explicit target.");
        Expect.True(inventory.Contains(item.InstanceId), $"{templateId} should not be consumed when targeting is missing.");
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        return content;
    }

    private static WorldState CreateWorld(IContentDatabase content)
    {
        var world = new WorldState { ContentDatabase = content };
        world.InitGrid(12, 12);
        world.Seed = 7;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(Position position, Stats? stats = null)
    {
        var actor = new StubEntity("Player", position, Faction.Player, stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        actor.SetComponent(new InventoryComponent());
        return actor;
    }

    private static ItemInstance AddItem(IEntity actor, string templateId)
    {
        var item = new ItemInstance { TemplateId = templateId, IsIdentified = true };
        actor.GetComponent<InventoryComponent>()!.Add(item);
        return item;
    }
}
