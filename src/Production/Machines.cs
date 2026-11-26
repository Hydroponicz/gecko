using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

public enum MachineKind
{
    Production,
    /// <summary>A landline for ordering supplies. Doesn't produce anything.</summary>
    Phone,
}

/// <summary>
/// Static data for a machine type (anything placed in build mode).
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
    bool OutputToStash,
    MachineKind Kind = MachineKind.Production);

/// <summary>
/// Counterfeit cash chain. Design rule: every machine holds up to 5 of each input (including what it's
/// working on) and 5 of its output, and you carry 5 of anything. Simple and easy to load; the game is
/// about immersion and fun, not ratio optimisation. Durations are placeholders to tune in play.
/// </summary>
public static class Machines
{
    public static readonly MachineDef PrintingPress = new(
        "printing_press", "Printing Press", new Vector2I(2, 2), 1.4f, new Color(0.35f, 0.38f, 0.45f),
        new Dictionary<ItemType, int> { [ItemType.Paper] = 1, [ItemType.Ink] = 1 },
        ItemType.WetSheet, OutputAmount: 1, Duration: 10f,
        Slots: 1, InputCapacity: 5, OutputCapacity: 5, OutputToStash: false);

    /// <summary>Hangs up to 5 sheets, each drying on its own timer.</summary>
    public static readonly MachineDef DryingRack = new(
        "drying_rack", "Drying Rack", new Vector2I(1, 3), 1.8f, new Color(0.5f, 0.42f, 0.3f),
        new Dictionary<ItemType, int> { [ItemType.WetSheet] = 1 },
        ItemType.DrySheet, OutputAmount: 1, Duration: 30f,
        Slots: 5, InputCapacity: 5, OutputCapacity: 5, OutputToStash: false);

    public static readonly MachineDef Cutter = new(
        "cutter", "Cutter", new Vector2I(1, 2), 1.1f, new Color(0.55f, 0.2f, 0.2f),
        new Dictionary<ItemType, int> { [ItemType.DrySheet] = 1 },
        ItemType.BillBundle, OutputAmount: 1, Duration: 5f,
        Slots: 1, InputCapacity: 5, OutputCapacity: 5, OutputToStash: false);

    /// <summary>Finished bricks go straight to the Stash.</summary>
    public static readonly MachineDef PackingTable = new(
        "packing_table", "Packing Table", new Vector2I(2, 1), 0.9f, new Color(0.45f, 0.35f, 0.25f),
        new Dictionary<ItemType, int> { [ItemType.BillBundle] = 5 },
        ItemType.CounterfeitBrick, OutputAmount: 1, Duration: 8f,
        Slots: 1, InputCapacity: 5, OutputCapacity: 5, OutputToStash: true);

    /// <summary>A wall phone for ordering supplies straight to this property.</summary>
    public static readonly MachineDef Landline = new(
        "landline", "Landline", new Vector2I(1, 1), 1.3f, new Color(0.15f, 0.15f, 0.15f),
        new Dictionary<ItemType, int>(), ItemType.Paper, OutputAmount: 0, Duration: 1f,
        Slots: 0, InputCapacity: 0, OutputCapacity: 0, OutputToStash: false, Kind: MachineKind.Phone);

    /// <summary>Everything placeable in build mode, in menu order.</summary>
    public static readonly IReadOnlyList<MachineDef> All = [PrintingPress, DryingRack, Cutter, PackingTable, Landline];
}
