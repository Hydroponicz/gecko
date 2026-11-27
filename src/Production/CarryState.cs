using System;
using System.Collections.Generic;
using System.Linq;
using Gecko.Economy;

namespace Gecko.Production;

/// <summary>What someone (player, later workers) is carrying: one item type at a time.</summary>
public sealed class CarryState
{
    public ItemType? Type { get; private set; }
    public int Count { get; private set; }
    public bool IsEmpty => Count == 0;

    private readonly List<int> _bags = [];

    /// <summary>Cash in each carried duffel bag.</summary>
    public IReadOnlyList<int> BagCash => _bags;
    public int TotalBagCash => _bags.Sum();

    /// <summary>Carrying the maximum number of duffel bags: no sprinting, jumping or (later) shooting.</summary>
    public bool IsEncumbered => Type == ItemType.DuffelBag && Count >= Items.Get(ItemType.DuffelBag).MaxCarry;

    public event Action? Changed;

    public int RoomFor(ItemType type)
    {
        if (IsEmpty)
            return Items.Get(type).MaxCarry;
        return Type == type ? Items.Get(type).MaxCarry - Count : 0;
    }

    /// <summary>Adds items. Duffel bags can't be added this way: use <see cref="AddCashToBags"/>.</summary>
    public int Add(ItemType type, int amount)
    {
        if (type == ItemType.DuffelBag)
            return 0;
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
        if (Type == ItemType.DuffelBag)
            _bags.RemoveRange(_bags.Count - removed, removed);
        Count -= removed;
        if (Count == 0)
            Type = null;
        Changed?.Invoke();
        return removed;
    }

    /// <summary>Puts cash into duffel bags: tops up carried bags, then picks up new ones (max 3).
    /// Only works with empty hands or while already carrying bags. Returns the amount added.</summary>
    public int AddCashToBags(int amount)
    {
        if (amount <= 0 || (!IsEmpty && Type != ItemType.DuffelBag))
            return 0;
        int added = 0;
        for (int i = 0; i < _bags.Count && added < amount; i++)
        {
            int put = Math.Min(Money.BagCapacity - _bags[i], amount - added);
            _bags[i] += put;
            added += put;
        }
        while (added < amount && Count < Items.Get(ItemType.DuffelBag).MaxCarry)
        {
            int put = Math.Min(Money.BagCapacity, amount - added);
            _bags.Add(put);
            Type = ItemType.DuffelBag;
            Count++;
            added += put;
        }
        if (added > 0)
            Changed?.Invoke();
        return added;
    }

    /// <summary>How much more cash could go into bags right now (top-ups plus new bags).</summary>
    public int AddCashToBagsRoom()
    {
        if (!IsEmpty && Type != ItemType.DuffelBag)
            return 0;
        int maxBags = Items.Get(ItemType.DuffelBag).MaxCarry;
        return (maxBags - Count) * Money.BagCapacity + _bags.Sum(b => Money.BagCapacity - b);
    }

    /// <summary>Picks up one whole duffel bag (e.g. from a trunk). False if hands are busy or 3 bags already.</summary>
    public bool AddBag(int cash)
    {
        if ((!IsEmpty && Type != ItemType.DuffelBag) || Count >= Items.Get(ItemType.DuffelBag).MaxCarry)
            return false;
        _bags.Add(cash);
        Type = ItemType.DuffelBag;
        Count++;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Puts down one duffel bag. Returns the cash that was in it.</summary>
    public int TakeBag()
    {
        if (Type != ItemType.DuffelBag || _bags.Count == 0)
            return 0;
        int cash = _bags[^1];
        Remove(1);
        return cash;
    }
}
