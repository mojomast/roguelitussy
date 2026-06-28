using System.Collections.Generic;
using System;

namespace Roguelike.Core;

public sealed record DialogueOption(
    string Text,
    string? NextNodeId,
    string? ActionId);

public sealed record DialogueNode(
    string NodeId,
    string Text,
    IReadOnlyList<DialogueOption> Options);

public sealed record DialogueTemplate(
    string TemplateId,
    string StartNodeId,
    IReadOnlyDictionary<string, DialogueNode> Nodes,
    IReadOnlyList<string> StartNodeIds)
{
    public DialogueTemplate(string templateId, string startNodeId, IReadOnlyDictionary<string, DialogueNode> nodes)
        : this(templateId, startNodeId, nodes, Array.Empty<string>())
    {
    }
}
