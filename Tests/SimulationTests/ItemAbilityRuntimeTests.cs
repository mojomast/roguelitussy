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
