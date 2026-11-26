using System;
using System.Globalization;
using Gecko.Production;

namespace Gecko.Economy;

/// <summary>Money rules. Small dirty cash is a number in your wallet; big amounts are physical.</summary>
public static class Money
{
    /// <summary>Most dirty cash you can keep in your wallet.</summary>
    public const int WalletCap = 5_000;

    /// <summary>Value of one physical cash stack (how Stash cash is shown).</summary>
    public const int StackValue = 1_000;

    /// <summary>Most cash one duffel bag holds. You can carry up to 3 bags.</summary>
    public const int BagCapacity = 10_000;

    /// <summary>Dirty cash you start with until robberies exist.</summary>
    public const int StartingDirty = 500;

    public static string Format(int amount) =>
        (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Pays dirty cash to someone (robbery haul, sale): wallet first, the rest goes into duffel bags
    /// they carry. Returns what didn't fit (nothing to carry it in).
    /// </summary>
    public static int ReceiveDirtyCash(Wallet wallet, CarryState carry, int amount)
    {
        int rest = amount - wallet.AddDirty(amount);
        rest -= carry.AddCashToBags(rest);
        return rest;
    }
}
