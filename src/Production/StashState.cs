using System;
using System.Collections.Generic;

namespace Gecko.Production;

public sealed class StashSlot
{
    public ItemType? Type { get; internal set; }
    public int Count { get; internal set; }
}

/// <summary>
/// The Stash at a facility entrance: a few shelf slots, each holding one item type.
/// Also holds dirty cash (<see cref="DirtyCash"/>).
/// </summary>
public sealed class StashState
{
    public const int SlotCapacity = 50;

    private readonly List<StashSlot> _slots = [];

    public StashState(int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
            _slots.Add(new StashSlot());
    }

    public IReadOnlyList<StashSlot> Slots => _slots;

    /// <summary>Dirty cash kept here, shown as physical $1,000 stacks. Wages will be paid from it.</summary>
    public int DirtyCash { get; internal set; }

    /// <summary>Adds to the slot already holding this type first, then to empty slots. Returns amount added.</summary>
    public int Add(ItemType type, int amount)
    {
        int remaining = amount;
        foreach (var slot in _slots)
        {
            if (slot.Type == type)
                remaining -= Fill(slot, type, remaining);
        }
        foreach (var slot in _slots)
        {
            if (remaining > 0 && slot.Type == null)
                remaining -= Fill(slot, type, remaining);
        }
        return amount - remaining;
    }

    public int Take(int slotIndex, int amount)
    {
        var slot = _slots[slotIndex];
        int taken = Math.Min(amount, slot.Count);
        slot.Count -= taken;
        if (slot.Count == 0)
            slot.Type = null;
        return taken;
    }

    public int RoomFor(ItemType type)
    {
        int room = 0;
        foreach (var slot in _slots)
        {
            if (slot.Type == type)
                room += SlotCapacity - slot.Count;
            else if (slot.Type == null)
                room += SlotCapacity;
        }
        return room;
    }

    private static int Fill(StashSlot slot, ItemType type, int amount)
    {
        int added = Math.Min(amount, SlotCapacity - slot.Count);
        if (added <= 0)
            return 0;
        slot.Type = type;
        slot.Count += added;
        return added;
    }
}
