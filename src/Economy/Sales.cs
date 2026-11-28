using System;
using System.Collections.Generic;
using System.Linq;
using Gecko.Production;
using Gecko.Vehicles;
using Godot;

namespace Gecko.Economy;

public enum ContactKind
{
    /// <summary>Small deals, high price; buys a limited amount, then goes quiet for a while.</summary>
    Buyer,
    /// <summary>Bulk deals, lower price, reliable; never on cooldown.</summary>
    Distributor,
}

/// <summary>A place a contact might meet you (world position).</summary>
public sealed record MeetingSpot(string Name, Vector3 Position);

/// <summary>Someone who buys product. Numbers are placeholders to tune.</summary>
public sealed class Contact(
    string name, ContactKind kind, int pricePerBrick, int minPerDeal, int maxPerDeal,
    int limitBeforeCooldown, double cooldownMinutes, IReadOnlyList<MeetingSpot> spots)
{
    public string Name { get; } = name;
    public ContactKind Kind { get; } = kind;
    public int PricePerBrick { get; } = pricePerBrick;
    public int MinPerDeal { get; } = minPerDeal;
    public int MaxPerDeal { get; } = maxPerDeal;
    /// <summary>Bricks bought before going on cooldown (0 = never).</summary>
    public int LimitBeforeCooldown { get; } = limitBeforeCooldown;
    public double CooldownMinutes { get; } = cooldownMinutes;
    public IReadOnlyList<MeetingSpot> Spots { get; } = spots;

    public int BoughtSinceCooldown { get; internal set; }
    /// <summary>Game minute when they'll buy again (0 = not on cooldown).</summary>
    public double CooldownUntil { get; internal set; }

    public bool OnCooldown(double now) => now < CooldownUntil;

    /// <summary>How many more bricks they'll take before going quiet.</summary>
    public int RemainingBeforeCooldown =>
        LimitBeforeCooldown == 0 ? int.MaxValue : Math.Max(0, LimitBeforeCooldown - BoughtSinceCooldown);
}

public enum DealPhase
{
    Scheduled, // agreed, waiting for the window to open
    Open,      // the contact is at the spot
    Done,      // sold (or the contact has bought all they will)
    Missed,    // the window closed before you sold anything
}

/// <summary>An arranged meeting: where, when, and how much they'll take.</summary>
public sealed class Deal(Contact contact, MeetingSpot spot, double windowStart, double windowEnd)
{
    public Contact Contact { get; } = contact;
    public MeetingSpot Spot { get; } = spot;
    public double WindowStart { get; } = windowStart;
    public double WindowEnd { get; } = windowEnd;
    public DealPhase Phase { get; internal set; } = DealPhase.Scheduled;
    public int BricksSold { get; internal set; }

    /// <summary>Most bricks still sellable in this deal.</summary>
    public int Remaining => Math.Min(Contact.MaxPerDeal - BricksSold, Contact.RemainingBeforeCooldown);

    /// <summary>Smallest sale accepted right now (distributors want bulk, but the first sale only).</summary>
    public int MinimumSale => BricksSold == 0 ? Contact.MinPerDeal : 1;

    public bool IsActive => Phase is DealPhase.Scheduled or DealPhase.Open;
}

/// <summary>
/// All selling: contacts, arranged deals, cooldowns and payment. Plain C# (ticked with game minutes),
/// so it can be simulated outside Godot. Arranging is done by phone; the contact decides where and when.
/// </summary>
public sealed class SalesState(IReadOnlyList<Contact> contacts, Random? random = null)
{
    public const double MinStartDelay = 20, MaxStartDelay = 60;     // game minutes until the window opens
    public const double MinWindow = 60, MaxWindow = 150;            // window length in game minutes

    private readonly List<Deal> _deals = [];
    private readonly Random _random = random ?? new Random();

    public IReadOnlyList<Contact> Contacts { get; } = contacts;
    public IReadOnlyList<Deal> Deals => _deals;
    public IEnumerable<Deal> ActiveDeals => _deals.Where(d => d.IsActive);

    public event Action<Deal>? DealChanged;

    public Deal? ActiveDealWith(Contact contact) => _deals.FirstOrDefault(d => d.Contact == contact && d.IsActive);

    /// <summary>Why a contact won't meet right now, or null if they will.</summary>
    public string? CantArrangeReason(Contact contact, double now)
    {
        if (ActiveDealWith(contact) != null)
            return "already meeting you";
        if (contact.OnCooldown(now))
            return $"not buying until {FormatTime(contact.CooldownUntil)}";
        return null;
    }

    /// <summary>Phone call: the contact picks a spot and a time window at random.</summary>
    public Deal? Arrange(Contact contact, double now)
    {
        if (CantArrangeReason(contact, now) != null)
            return null;
        var spot = contact.Spots[_random.Next(contact.Spots.Count)];
        double start = now + Lerp(MinStartDelay, MaxStartDelay);
        double end = start + Lerp(MinWindow, MaxWindow);
        var deal = new Deal(contact, spot, start, end);
        _deals.Add(deal);
        DealChanged?.Invoke(deal);
        return deal;
    }

    public void Tick(double now)
    {
        foreach (var deal in _deals.Where(d => d.IsActive).ToList())
        {
            if (deal.Phase == DealPhase.Scheduled && now >= deal.WindowStart)
            {
                deal.Phase = DealPhase.Open;
                DealChanged?.Invoke(deal);
            }
            if (now >= deal.WindowEnd)
            {
                deal.Phase = deal.BricksSold > 0 ? DealPhase.Done : DealPhase.Missed;
                DealChanged?.Invoke(deal);
            }
        }
        _deals.RemoveAll(d => !d.IsActive && now > d.WindowEnd + 60); // forget old deals
    }

    /// <summary>
    /// Most bricks you can sell right now: limited by what you have, what they'll take,
    /// and how much cash you can physically take away (wallet + bag room).
    /// </summary>
    public static int MaxSellable(Deal deal, int bricksAvailable, int cashRoom) =>
        Math.Max(0, Math.Min(Math.Min(deal.Remaining, bricksAvailable), cashRoom / deal.Contact.PricePerBrick));

    /// <summary>
    /// Sells bricks: removes them via <paramref name="takeBricks"/>, pays via <paramref name="pay"/>,
    /// updates the contact's cooldown. Returns bricks sold.
    /// </summary>
    public int Sell(Deal deal, int wanted, double now, Func<int, int> takeBricks, Action<int> pay)
    {
        if (deal.Phase != DealPhase.Open || wanted < deal.MinimumSale)
            return 0;
        int sold = takeBricks(Math.Min(wanted, deal.Remaining));
        if (sold <= 0)
            return 0;
        pay(sold * deal.Contact.PricePerBrick);
        deal.BricksSold += sold;
        var contact = deal.Contact;
        contact.BoughtSinceCooldown += sold;
        if (contact.LimitBeforeCooldown > 0 && contact.BoughtSinceCooldown >= contact.LimitBeforeCooldown)
        {
            contact.BoughtSinceCooldown = 0;
            contact.CooldownUntil = now + contact.CooldownMinutes;
            deal.Phase = DealPhase.Done; // they've bought all they will for now
        }
        else if (deal.Remaining <= 0)
        {
            deal.Phase = DealPhase.Done;
        }
        DealChanged?.Invoke(deal);
        return sold;
    }

    public static string FormatTime(double totalMinutes)
    {
        int day = (int)(totalMinutes / (24 * 60)) + 1;
        int minute = (int)(totalMinutes % (24 * 60));
        return $"Day {day} {minute / 60:00}:{minute % 60:00}";
    }

    private double Lerp(double min, double max) => min + _random.NextDouble() * (max - min);
}

/// <summary>Where sale money goes: wallet first, then duffel bags in the trunk (if selling from a car), then bags in your hands.</summary>
public static class Payment
{
    public static int Room(Wallet wallet, CarryState hands, CargoState? trunk) =>
        wallet.DirtyRoom + (trunk?.CashRoom ?? 0) + hands.AddCashToBagsRoom();

    public static void Pay(int amount, Wallet wallet, CarryState hands, CargoState? trunk)
    {
        int rest = amount - wallet.AddDirty(amount);
        if (trunk != null)
            rest -= trunk.AddCash(rest);
        hands.AddCashToBags(rest);
    }
}
