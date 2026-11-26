using System.Collections.Generic;
using Gecko.Production;

namespace Gecko.Economy;

/// <summary>The paper and ink supplier you call from a phone. Prices are dirty-money prices (placeholders).</summary>
public static class Supplier
{
    public const int OrderSize = 5;
    public const float DeliverySeconds = 30f;

    private static readonly Dictionary<ItemType, int> DirtyPrices = new()
    {
        [ItemType.Paper] = 20,
        [ItemType.Ink] = 20,
    };

    public static int Price(ItemType type) => DirtyPrices[type];

    public static int OrderCost(IEnumerable<ItemType> types)
    {
        int total = 0;
        foreach (var type in types)
            total += Price(type) * OrderSize;
        return total;
    }
}
