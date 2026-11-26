using System.Collections.Generic;
using System.Linq;
using Gecko.Economy;

namespace Gecko.Production;

/// <summary>The supplier call menu, shared by landlines and pay phones.</summary>
public static class PhoneMenu
{
    private static readonly ItemType[][] Orders =
    [
        [ItemType.Paper],
        [ItemType.Ink],
        [ItemType.Paper, ItemType.Ink],
    ];

    /// <param name="facility">The property the order is delivered to (null if you own none).</param>
    public static IReadOnlyList<InteractOption> GetOptions(FacilityNode? facility, Interactor who)
    {
        if (facility == null)
            return [new InteractOption("Order supplies", false, "you don't own a property", _ => 0)];

        var options = new List<InteractOption>();
        foreach (var order in Orders)
        {
            int cost = Supplier.OrderCost(order);
            string what = order.Length == 1
                ? $"{Supplier.OrderSize} {Items.Get(order[0]).Name}"
                : $"{Supplier.OrderSize} of each: {string.Join(" + ", order.Select(t => Items.Get(t).Name))}";
            bool affordable = who.Wallet.Dirty >= cost;
            options.Add(new InteractOption($"Order {what}", affordable,
                affordable ? Money.Format(cost) : $"{Money.Format(cost)} - not enough cash",
                _ => facility.State.OrderSupplies(who.Wallet, order) ? 1 : 0));
        }
        return options;
    }

    public static string Status(FacilityNode? facility)
    {
        if (facility == null)
            return "No property to deliver to";
        var deliveries = facility.State.Deliveries;
        if (deliveries.Count == 0)
            return $"Delivers to {facility.DisplayName}'s Stash";
        float soonest = deliveries.Min(d => d.Remaining);
        int seconds = (int)System.Math.Ceiling(System.Math.Max(0f, soonest));
        string items = string.Join(", ", deliveries.Select(d => $"{d.Amount} {Items.Get(d.Type).Name}"));
        return $"Delivery in {seconds / 60}:{seconds % 60:00}  ({items})";
    }
}
