using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed record EquippedItem(ItemInstance Item, EquipSlot Slot, IReadOnlyDictionary<string, int> StatModifiers);

public sealed class InventoryComponent
{
    private readonly List<ItemInstance> _items = new();
    private readonly Dictionary<EquipSlot, EquippedItem> _equipped = new();

    public InventoryComponent(int capacity = 26)
    {
        Capacity = Math.Max(0, capacity);
    }

    public int Capacity { get; }

    public IReadOnlyList<ItemInstance> Items => _items;

    public IReadOnlyDictionary<EquipSlot, EquippedItem> EquippedItems => _equipped;

    public bool HasSpace => _items.Count < Capacity;

    public bool Contains(EntityId instanceId) => _items.Any(item => item.InstanceId == instanceId);

    public ItemInstance? Get(EntityId instanceId) => _items.FirstOrDefault(item => item.InstanceId == instanceId);

    public bool Add(ItemInstance item)
    {
        if (!HasSpace)
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public bool Remove(EntityId instanceId, out ItemInstance? item)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].InstanceId != instanceId)
            {
                continue;
            }

            item = _items[i];
            _items.RemoveAt(i);
            if (item is not null)
            {
                foreach (var pair in _equipped)
                {
                    if (pair.Value.Item.InstanceId == item.InstanceId)
                    {
                        _equipped.Remove(pair.Key);
                        break;
                    }
                }
            }

            return true;
        }

        item = null;
        return false;
    }

    public bool IsEquipped(EntityId instanceId) => _equipped.Values.Any(entry => entry.Item.InstanceId == instanceId);

    public EquippedItem? GetEquipped(EquipSlot slot) => _equipped.TryGetValue(slot, out var equipped) ? equipped : null;

    public bool TryEquip(ItemInstance item, EquipSlot slot, IReadOnlyDictionary<string, int> statModifiers, out EquippedItem? previous)
    {
        previous = null;
        if (slot == EquipSlot.None || !Contains(item.InstanceId))
        {
            return false;
        }

        if (_equipped.TryGetValue(slot, out var existing))
        {
            previous = existing;
        }

        _equipped[slot] = new EquippedItem(item, slot, new Dictionary<string, int>(statModifiers));
        return true;
    }

    public bool TryUnequip(EquipSlot slot, out EquippedItem? removed)
    {
        if (_equipped.TryGetValue(slot, out var equipped))
        {
            _equipped.Remove(slot);
            removed = equipped;
            return true;
        }

        removed = null;
        return false;
    }

    public EquipSlot GetEquippedSlot(EntityId instanceId)
    {
        foreach (var pair in _equipped)
        {
            if (pair.Value.Item.InstanceId == instanceId)
            {
                return pair.Key;
            }
        }

        return EquipSlot.None;
    }
}