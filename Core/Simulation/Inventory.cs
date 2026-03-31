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

    public bool CanAccept(ItemInstance item, int maxStack)
    {
        if (maxStack <= 1)
        {
            return HasSpace;
        }

        var remaining = Math.Max(1, item.StackCount);
        foreach (var existing in _items)
        {
            if (!CanStack(existing, item) || existing.StackCount >= maxStack)
            {
                continue;
            }

            remaining -= maxStack - existing.StackCount;
            if (remaining <= 0)
            {
                return true;
            }
        }

        var stacksNeeded = (remaining + maxStack - 1) / maxStack;
        return _items.Count + stacksNeeded <= Capacity;
    }

    public bool AddWithStacking(ItemInstance item, int maxStack)
    {
        if (maxStack <= 1)
        {
            return Add(item);
        }

        if (!CanAccept(item, maxStack))
        {
            return false;
        }

        var remaining = Math.Max(1, item.StackCount);
        foreach (var existing in _items)
        {
            if (!CanStack(existing, item) || existing.StackCount >= maxStack)
            {
                continue;
            }

            var transfer = Math.Min(maxStack - existing.StackCount, remaining);
            existing.StackCount += transfer;
            remaining -= transfer;
            if (remaining <= 0)
            {
                return true;
            }
        }

        var firstStack = Math.Min(maxStack, remaining);
        item.StackCount = firstStack;
        _items.Add(item);
        remaining -= firstStack;

        while (remaining > 0)
        {
            var stackCount = Math.Min(maxStack, remaining);
            _items.Add(CloneItem(item, stackCount));
            remaining -= stackCount;
        }

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

    public bool RemoveQuantity(EntityId instanceId, int quantity, out ItemInstance? item)
    {
        if (quantity <= 0)
        {
            item = null;
            return false;
        }

        var existing = Get(instanceId);
        if (existing is null)
        {
            item = null;
            return false;
        }

        if (quantity >= existing.StackCount || existing.StackCount <= 1)
        {
            return Remove(instanceId, out item);
        }

        existing.StackCount -= quantity;
        item = CloneItem(existing, quantity);
        return true;
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

    private static bool CanStack(ItemInstance left, ItemInstance right)
    {
        return left.TemplateId == right.TemplateId
            && left.IsIdentified == right.IsIdentified
            && left.CurrentCharges == right.CurrentCharges;
    }

    private static ItemInstance CloneItem(ItemInstance source, int stackCount)
    {
        return new ItemInstance
        {
            InstanceId = EntityId.New(),
            TemplateId = source.TemplateId,
            CurrentCharges = source.CurrentCharges,
            StackCount = stackCount,
            IsIdentified = source.IsIdentified,
        };
    }
}