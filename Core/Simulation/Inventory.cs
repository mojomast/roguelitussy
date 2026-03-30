using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core.Simulation;

public sealed class Inventory
{
    public const int MaxItems = 20;

    public List<ItemInstance> Items { get; } = new();

    public bool IsFull => Items.Count >= MaxItems;

    public bool Add(ItemInstance item)
    {
        if (IsFull)
            return false;
        Items.Add(item);
        return true;
    }

    public bool Remove(EntityId instanceId)
    {
        int idx = Items.FindIndex(i => i.InstanceId == instanceId);
        if (idx < 0)
            return false;
        Items.RemoveAt(idx);
        return true;
    }

    public ItemInstance? GetByTemplateId(string templateId) =>
        Items.FirstOrDefault(i => i.TemplateId == templateId);
}
