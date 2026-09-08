using System;
using System.IO;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class NpcDialoguePersistenceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.NpcDialogue treatment and derived dialog state survive reload", TreatmentRoundTrip);
    }

    private static void TreatmentRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "roguelitussy-npc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var content = ContentLoader.LoadFromRepository();
            var world = new WorldState { Seed = 41, ContentDatabase = content };
            world.InitGrid(4, 4);
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 4; x++)
                {
                    world.SetTile(new Position(x, y), TileType.Floor);
                }
            }

            var player = new Entity("Patient", new Position(1, 1), new Stats { HP = 10, MaxHP = 30, Speed = 100 }, Faction.Player);
            player.SetComponent(new WalletComponent { Gold = 40 });
            player.SetComponent(new InventoryComponent());
            var faction = new FactionComponent();
            faction.Reputation["merchants_guild"] = 30;
            player.SetComponent(faction);
            world.Player = player;
            world.AddEntity(player);
            var npc = new Entity("Vale", new Position(2, 1), new Stats { HP = 1, MaxHP = 1, Speed = 100 }, Faction.Neutral);
            npc.SetComponent(new NpcComponent { TemplateId = "quartermaster_vale", Role = "shopkeeper", DialogueId = "merchant_intro" });
            world.AddEntity(npc);
            Expect.Equal(ActionResult.Success, new NpcServiceAction(player.Id, npc.Id, "field_dressing").Execute(world).Result, "Initial treatment should succeed.");
            var node = content.DialogueTemplates["merchant_intro"].Nodes["start"];
            var choices = string.Join("|", DialogueResolver.GetOptions(node, player).Select(option => option.Text));
            var report = DialogueResolver.BuildExpeditionReport(world);
            var save = new SaveManager(directory, () => new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));
            Expect.True(save.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Current save format should accept treated player/NPC state.");
            var restored = save.LoadGame(SaveSlots.Slot1, content).GetAwaiter().GetResult();
            Expect.NotNull(restored, "NPC state should load with content binding.");
            Expect.Equal(25, restored!.Player.Stats.HP, "Treatment HP should persist.");
            Expect.Equal(28, restored.Player.GetComponent<WalletComponent>()!.Gold, "Payment should persist.");
            Expect.Equal(choices, string.Join("|", DialogueResolver.GetOptions(node, restored.Player).Select(option => option.Text)), "Derived conditions should be unchanged by reload.");
            Expect.Equal(report, DialogueResolver.BuildExpeditionReport(restored), "Read-only report should be reproducible after reload.");
            Expect.Equal(world.CombatRandomState, restored.CombatRandomState, "Treatment should preserve saved combat RNG.");
            Expect.Equal(world.ItemRandomState, restored.ItemRandomState, "Treatment should preserve saved item RNG.");
            Expect.Equal(ActionResult.Success, new NpcServiceAction(restored.Player.Id, npc.Id, "field_dressing").Execute(restored).Result, "Remaining wounds may be treated again without service flags.");
            Expect.Equal(30, restored.Player.Stats.HP, "Second treatment should cap at full health.");
            Expect.Equal(16, restored.Player.GetComponent<WalletComponent>()!.Gold, "Second treatment pays exactly once.");
            Expect.Equal(ActionResult.Blocked, new NpcServiceAction(restored.Player.Id, npc.Id, "field_dressing").Execute(restored).Result, "Full health must still block repeat treatment after reload.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
