using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class MerchantOfferState
{
    public string ItemTemplateId { get; set; } = string.Empty;

    public int Price { get; set; }

    public int Quantity { get; set; }
}

public sealed class MerchantComponent
{
    public MerchantComponent()
    {
    }

    public MerchantComponent(IEnumerable<MerchantOfferState> offers)
    {
        Offers.AddRange(offers.Select(offer => new MerchantOfferState
        {
            ItemTemplateId = offer.ItemTemplateId,
            Price = offer.Price,
            Quantity = offer.Quantity,
        }));
    }

    public List<MerchantOfferState> Offers { get; } = new();
}