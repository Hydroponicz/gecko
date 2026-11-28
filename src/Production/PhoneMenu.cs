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

    /// <summary>
    /// Phone main menu: call the supplier, hire a worker, arrange a sale.
    /// </summary>
    /// <param name="facility">Your property: supplies and workers go there (null if you own none).</param>
    public static IReadOnlyList<InteractOption> GetOptions(FacilityNode? facility, Interactor who)
    {
        var options = new List<InteractOption>();
        if (facility == null)
        {
            options.Add(new InteractOption("Call the supplier", false, "you don't own a property", _ => 0));
            options.Add(new InteractOption("Hire a worker", false, "you don't own a property", _ => 0));
        }
        else
        {
            options.Add(InteractOption.Menu("Call the supplier", "paper & ink", () => SupplierOptions(facility, who)));
            options.Add(HireOption(facility));
        }
        if (SalesService.Current is { } sales)
            options.Add(InteractOption.Menu("Arrange a sale", SaleSummary(sales), () => SaleOptions(sales)));
        return options;
    }

    private static IReadOnlyList<InteractOption> SupplierOptions(FacilityNode facility, Interactor who)
    {
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

    private static InteractOption HireOption(FacilityNode facility)
    {
        // Cap depends on the layout's jobs and the building size.
        var state = facility.State;
        int hired = state.Workers.Count(w => w.Phase != WorkerPhase.Leaving);
        int cap = state.WorkerCap;
        string detail = cap == 0 ? "place machines first"
            : hired >= cap ? $"{hired}/{cap} - full for this layout"
            : $"{Money.Format(FacilityState.WorkerWage)}/day - {hired}/{cap}";
        return new InteractOption("Hire a worker", hired < cap, detail, _ => state.HireWorker() != null ? 1 : 0);
    }

    private static string SaleSummary(SalesState sales)
    {
        int active = sales.ActiveDeals.Count();
        return active == 0 ? "buyers & distributors" : $"{active} meeting(s) arranged";
    }

    /// <summary>One entry per contact; the contact decides where and when to meet.</summary>
    private static IReadOnlyList<InteractOption> SaleOptions(SalesState sales)
    {
        double now = SalesService.Now();
        var options = new List<InteractOption>();
        foreach (var contact in sales.Contacts)
        {
            string? reason = sales.CantArrangeReason(contact, now);
            string kind = contact.Kind == ContactKind.Buyer
                ? $"buys {contact.MinPerDeal}-{contact.MaxPerDeal} at {Money.Format(contact.PricePerBrick)}"
                : $"bulk {contact.MinPerDeal}+ at {Money.Format(contact.PricePerBrick)}";
            options.Add(new InteractOption($"Call {contact.Name}", reason == null, reason ?? kind,
                _ => sales.Arrange(contact, SalesService.Now()) != null ? 1 : 0));
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
