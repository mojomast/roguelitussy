using System.Collections.Generic;

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
    IReadOnlyDictionary<string, DialogueNode> Nodes);