using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record MerchantOfferTemplate(
    string ItemTemplateId,
    int Price,
    int Quantity);

public sealed record NpcTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    string Role,
    int MinDepth,
    int MaxDepth,
    string DialogueId,
    string RaceId,
    string GenderId,
    string AppearanceId,
    string ArchetypeId,
    IReadOnlyList<MerchantOfferTemplate>? MerchantOffers = null)
{
    public bool IsMerchant => MerchantOffers is { Count: > 0 };
}