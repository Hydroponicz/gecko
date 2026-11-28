using System;
using System.Collections.Generic;
using System.Linq;
using Gecko.Production;

namespace Gecko.Vehicles;

/// <summary>
/// A vehicle's trunk: general storage measured in "space". Most items take 1, a duffel bag 6.
/// Built as a general container so weapons can be stored here later.
/// </summary>
public sealed class CargoState(int capacity)
{
    private readonly Dictionary<ItemType, int> _items = new();
    private readonly List<int> _bags = [];

    public int Capacity { get; } = capacity;
    public IReadOnlyDictionary<ItemType, int> Items => _items;
    public IReadOnlyList<int> Bags => _bags;
    public int BagCash => _bags.Sum();

    public event Action? Changed;

    public static int SpaceOf(ItemType type) => type == ItemType.DuffelBag ? 6 : 1;

    public int SpaceUsed => _items.Sum(kv => kv.Value * SpaceOf(kv.Key)) + _bags.Count * SpaceOf(ItemType.DuffelBag);
    public int RoomFor(ItemType type) => (Capacity - SpaceUsed) / SpaceOf(type);

    /// <summary>Puts carried items (or bags) into the trunk. Returns how many moved.</summary>
    public int LoadFrom(CarryState carry, int amount)
    {
        if (carry.Type is not { } type)
            return 0;
        int count = Math.Min(Math.Min(amount, carry.Count), RoomFor(type));
        if (count <= 0)
            return 0;
        if (type == ItemType.DuffelBag)
        {
            for (int i = 0; i < count; i++)
                _bags.Add(carry.TakeBag());
        }
        else
        {
            carry.Remove(count);
            _items[type] = _items.GetValueOrDefault(type) + count;
        }
        Changed?.Invoke();
        return count;
    }

    /// <summary>How much cash could still go into the trunk as new duffel bags.</summary>
    public int CashRoom => RoomFor(ItemType.DuffelBag) * Gecko.Economy.Money.BagCapacity;

    /// <summary>Puts cash into the trunk as new duffel bags (up to $10,000 each). Returns the amount stored.</summary>
    public int AddCash(int amount)
    {
        int added = 0;
        while (added < amount && RoomFor(ItemType.DuffelBag) > 0)
        {
            int bag = Math.Min(Gecko.Economy.Money.BagCapacity, amount - added);
            _bags.Add(bag);
            added += bag;
        }
        if (added > 0)
            Changed?.Invoke();
        return added;
    }

    /// <summary>Removes items for a sale (no hands involved). Returns how many were removed.</summary>
    public int TakeItems(ItemType type, int amount)
    {
        int taken = Math.Min(amount, _items.GetValueOrDefault(type));
        if (taken <= 0)
            return 0;
        _items[type] -= taken;
        if (_items[type] == 0)
            _items.Remove(type);
        Changed?.Invoke();
        return taken;
    }

    /// <summary>Takes items (or bags) out into someone's hands. Returns how many moved.</summary>
    public int UnloadTo(CarryState carry, ItemType type, int amount)
    {
        int moved = 0;
        if (type == ItemType.DuffelBag)
        {
            while (moved < amount && _bags.Count > 0 && carry.AddBag(_bags[^1]))
            {
                _bags.RemoveAt(_bags.Count - 1);
                moved++;
            }
        }
        else
        {
            moved = carry.Add(type, Math.Min(amount, _items.GetValueOrDefault(type)));
            if (moved > 0)
            {
                _items[type] -= moved;
                if (_items[type] == 0)
                    _items.Remove(type);
            }
        }
        if (moved > 0)
            Changed?.Invoke();
        return moved;
    }
}
