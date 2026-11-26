using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>
/// Static data for a machine type.
/// A machine runs up to <see cref="Slots"/> crafts at once. Each craft consumes <see cref="Inputs"/>
/// and after <see cref="Duration"/> seconds produces <see cref="OutputAmount"/> of <see cref="Output"/>.
/// </summary>
public sealed record MachineDef(
    string Id,
    string Name,
    Vector2I Footprint,
    float Height,
    Color Color,
    IReadOnlyDictionary<ItemType, int> Inputs,
    ItemType Output,
    int OutputAmount,
    float Duration,
    int Slots,
    int InputCapacity,
    int OutputCapacity,
    bool OutputToStash);

/// <summary>Counterfeit cash chain. All numbers are placeholders to tune in play.</summary>
public static class Machines
{
    public static readonly MachineDef PrintingPress = new(
        "printing_press", "Printing Press", new Vector2I(2, 2), 1.4f, new Color(0.35f, 0.38f, 0.45f),
        new Dictionary<ItemType, int> { [ItemType.Paper] = 1, [ItemType.Ink] = 1 },
        ItemType.WetSheet, OutputAmount: 1, Duration: 10f,
        Slots: 1, InputCapacity: 5, OutputCapacity: 5, OutputToStash: false);

    /// <summary>Capacity puzzle: holds 6 sheets, each drying on its own timer.</summary>
    public static readonly MachineDef DryingRack = new(
        "drying_rack", "Drying Rack", new Vector2I(1, 3), 1.8f, new Color(0.5f, 0.42f, 0.3f),
        new Dictionary<ItemType, int> { [ItemType.WetSheet] = 1 },
        ItemType.DrySheet, OutputAmount: 1, Duration: 30f,
        Slots: 6, InputCapacity: 1, OutputCapacity: 6, OutputToStash: false);

    public static readonly MachineDef Cutter = new(
        "cutter", "Cutter", new Vector2I(1, 2), 1.1f, new Color(0.55f, 0.2f, 0.2f),
        new Dictionary<ItemType, int> { [ItemType.DrySheet] = 1 },
        ItemType.LooseBills, OutputAmount: 4, Duration: 5f,
        Slots: 1, InputCapacity: 5, OutputCapacity: 20, OutputToStash: false);

    /// <summary>Finished bricks go straight to the Stash.</summary>
    public static readonly MachineDef PackingTable = new(
        "packing_table", "Packing Table", new Vector2I(2, 1), 0.9f, new Color(0.45f, 0.35f, 0.25f),
        new Dictionary<ItemType, int> { [ItemType.LooseBills] = 10 },
        ItemType.CounterfeitBrick, OutputAmount: 1, Duration: 8f,
        Slots: 1, InputCapacity: 20, OutputCapacity: 4, OutputToStash: true);

    public static readonly IReadOnlyList<MachineDef> All = [PrintingPress, DryingRack, Cutter, PackingTable];
}
