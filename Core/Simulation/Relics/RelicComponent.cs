using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class RelicComponent
{
    public List<string> RelicIds { get; } = new();

    public int ShieldCharges { get; set; }

    public bool LowHpRelicFired { get; set; }

    public HashSet<EntityId> FirstHitEntityIds { get; } = new();

    public HashSet<string> AppliedOneTimeRelics { get; } = new();

    public Dictionary<string, int> AppliedStatTotals { get; } = new(System.StringComparer.Ordinal);

    public int DamageBuffPercent { get; set; }

    public int DamageBuffExpiresOnTurn { get; set; }

    public int LastMerchantDiscountDepth { get; set; } = -1;

    public bool HasRelic(string relicId) => RelicIds.Contains(relicId);

    public bool AddRelic(string relicId, RelicTemplate? template = null)
    {
        if (string.IsNullOrWhiteSpace(relicId))
        {
            return false;
        }

        if ((template?.IsUnique ?? true) && HasRelic(relicId))
        {
            return false;
        }

        RelicIds.Add(relicId);
        return true;
    }
}
