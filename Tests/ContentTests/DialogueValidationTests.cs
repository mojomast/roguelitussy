using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.ContentTests;

public sealed class DialogueValidationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Content.Dialogue authored service and conditions project", AuthoredCapabilitiesProject);
        registry.Add("Content.Dialogue rejects ambiguous and missing outcomes", RejectsInvalidOutcomes);
        registry.Add("Content.Dialogue rejects unsupported and malformed conditions", RejectsInvalidConditions);
        registry.Add("Content.Dialogue rejects unauthorized and invalid services", RejectsInvalidServices);
        registry.Add("Content.Dialogue authored nodes retain unconditional exits", AuthoredExits);
    }

    private static void AuthoredCapabilitiesProject()
    {
        var content = ContentLoader.LoadFromRepository();
        var vale = content.NpcTemplates["quartermaster_vale"];
        var service = vale.Services!.Single();
        Expect.Equal("field_dressing", service.Id, "Project the authorized service id.");
        Expect.Equal(12, service.Cost, "Project the authored cost.");
        Expect.Equal(15, service.HealAmount, "Project the authored healing.");
        Expect.True(content.NpcTemplates["field_chronicler"].Services?.Count == 0, "Sen must not authorize field dressing.");
        var options = content.DialogueTemplates.Values.SelectMany(dialog => dialog.Nodes.Values).SelectMany(node => node.Options).ToArray();
        foreach (var type in new[] { "injured", "missing_item", "reputation_at_least" })
        {
            Expect.True(options.Any(option => option.Condition?.Type == type), $"Project authored {type} branches.");
        }

        Expect.True(options.Any(option => option.ActionId == "report"), "Sen should have a supported report action.");
    }

    private static void RejectsInvalidOutcomes()
    {
        AssertInvalid("dialogs.json", root => FirstOption(root)["next"] = "advice", "exactly one");
        AssertInvalid("dialogs.json", root => FirstOption(root).AsObject().Remove("action"), "exactly one");
        AssertInvalid("dialogs.json", root => FirstOption(root)["action"] = "reward", "unknown action");
    }

    private static void RejectsInvalidConditions()
    {
        foreach (var json in new[]
        {
            "{\"type\":\"quest_complete\"}",
            "{\"type\":\"missing_item\",\"item_id\":\"not_an_item\"}",
            "{\"type\":\"reputation_at_least\",\"faction_id\":\"not_a_faction\",\"value\":20}",
            "{\"type\":\"reputation_at_least\",\"faction_id\":\"warriors_order\"}",
            "{\"type\":\"injured\",\"value\":5}",
        })
        {
            AssertInvalid("dialogs.json", root => FirstOption(root)["condition"] = JsonNode.Parse(json), "invalid condition");
        }
    }

    private static void RejectsInvalidServices()
    {
        AssertInvalid("npcs.json", root => root["npcs"]![0]!["services"]![0]!["cost"] = 0, "invalid or duplicate service");
        AssertInvalid("npcs.json", root => root["npcs"]![0]!["services"]![0]!["heal_amount"] = -1, "invalid or duplicate service");
        AssertInvalid("npcs.json", root => root["npcs"]![0]!["services"]![0]!["id"] = "resurrection", "invalid or duplicate service");
        AssertInvalid("npcs.json", root => root["npcs"]![0]!["services"]!.AsArray().Add(root["npcs"]![0]!["services"]![0]!.DeepClone()), "duplicate service");
        AssertInvalid("npcs.json", root => root["npcs"]![0]!["services"] = new JsonArray(), "unauthorized service");
        AssertInvalid("npcs.json", root => root["npcs"]![1]!["dialogue_id"] = "merchant_intro", "requests shop");
    }

    private static void AuthoredExits()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var dialog in content.DialogueTemplates.Values)
        {
            foreach (var node in dialog.Nodes.Values)
            {
                Expect.True(node.Options.Any(option => option.ActionId == "close" && option.Condition is null), $"{dialog.TemplateId}/{node.NodeId} needs an unconditional exit.");
                foreach (var option in node.Options)
                {
                    Expect.True((option.NextNodeId is null) != (option.ActionId is null), "Every authored option should have one outcome.");
                }
            }
        }
    }

    private static JsonNode FirstOption(JsonNode root) => root["dialogs"]![0]!["nodes"]![0]!["options"]![0]!;

    private static void AssertInvalid(string file, Action<JsonNode> mutate, string expected)
    {
        var directory = ContentLoader.FindContentDirectory();
        var documents = ContentLoader.RequiredFileNames.ToDictionary(name => name, name => File.ReadAllText(Path.Combine(directory, name)));
        var root = JsonNode.Parse(documents[file])!;
        mutate(root);
        documents[file] = root.ToJsonString();
        var content = ContentLoader.LoadFromJsonDocuments("npc-validation", documents, throwOnValidationErrors: false);
        Expect.False(content.IsValid, "Invalid authored capabilities must fail validation.");
        Expect.True(content.ValidationErrors.Any(error => error.Contains(expected, StringComparison.OrdinalIgnoreCase)), $"Expected diagnostic containing '{expected}'.");
    }
}
