using System;

namespace Gecko.Economy;

/// <summary>
/// Money a person has on them: dirty cash in their wallet (a number, capped) and clean money in the bank.
/// Larger amounts of dirty cash only exist physically (Stash piles, duffel bags).
/// </summary>
public sealed class Wallet(int startingDirty)
{
    public int Dirty { get; private set; } = Math.Clamp(startingDirty, 0, Money.WalletCap);
    public int Clean { get; private set; }
    public int DirtyRoom => Money.WalletCap - Dirty;

    /// <summary>Raised with (dirty change, clean change).</summary>
    public event Action<int, int>? Changed;

    /// <summary>Adds what fits under the cap. Returns the amount added.</summary>
    public int AddDirty(int amount)
    {
        int added = Math.Clamp(amount, 0, DirtyRoom);
        if (added > 0)
        {
            Dirty += added;
            Changed?.Invoke(added, 0);
        }
        return added;
    }

    /// <summary>Takes up to <paramref name="amount"/>. Returns the amount taken.</summary>
    public int TakeDirty(int amount)
    {
        int taken = Math.Clamp(amount, 0, Dirty);
        if (taken > 0)
        {
            Dirty -= taken;
            Changed?.Invoke(-taken, 0);
        }
        return taken;
    }

    /// <summary>Pays an exact price, or nothing if you can't afford it.</summary>
    public bool SpendDirty(int amount)
    {
        if (amount < 0 || amount > Dirty)
            return false;
        TakeDirty(amount);
        return true;
    }

    public void AddClean(int amount)
    {
        if (amount <= 0)
            return;
        Clean += amount;
        Changed?.Invoke(0, amount);
    }
}
