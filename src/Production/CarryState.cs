using System;

namespace Gecko.Production;

/// <summary>What someone (player, later workers) is carrying: one item type at a time.</summary>
public sealed class CarryState
{
    public ItemType? Type { get; private set; }
    public int Count { get; private set; }
    public bool IsEmpty => Count == 0;

    public event Action? Changed;

    public int RoomFor(ItemType type)
    {
        if (IsEmpty)
            return Items.Get(type).MaxCarry;
        return Type == type ? Items.Get(type).MaxCarry - Count : 0;
    }

    public int Add(ItemType type, int amount)
    {
        int added = Math.Min(amount, RoomFor(type));
        if (added <= 0)
            return 0;
        Type = type;
        Count += added;
        Changed?.Invoke();
        return added;
    }

    public int Remove(int amount)
    {
        int removed = Math.Min(amount, Count);
        if (removed <= 0)
            return 0;
        Count -= removed;
        if (Count == 0)
            Type = null;
        Changed?.Invoke();
        return removed;
    }
}
