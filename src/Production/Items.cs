using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

public enum ItemType
{
    Paper,
    Ink,
    WetSheet,
    DrySheet,
    LooseBills,
    CounterfeitBrick,
}

public enum ItemShape
{
    Box,
    Cylinder,
}

/// <summary>
/// Static data for an item. <see cref="Shape"/>, <see cref="Size"/> and <see cref="Color"/> describe
/// the placeholder look; real models can replace them later.
/// </summary>
public sealed record ItemDef(
    ItemType Type,
    string Name,
    int MaxCarry,
    ItemShape Shape,
    Vector3 Size,
    Color Color);

public static class Items
{
    private static readonly Dictionary<ItemType, ItemDef> Defs = new()
    {
        // A ream of paper
        [ItemType.Paper] = new(ItemType.Paper, "Paper", 5, ItemShape.Box,
            new Vector3(0.30f, 0.06f, 0.22f), new Color(0.95f, 0.95f, 0.92f)),
        // A canister of ink
        [ItemType.Ink] = new(ItemType.Ink, "Ink", 5, ItemShape.Cylinder,
            new Vector3(0.16f, 0.15f, 0.16f), new Color(0.1f, 0.12f, 0.3f)),
        // Freshly printed sheet, glossy and pale
        [ItemType.WetSheet] = new(ItemType.WetSheet, "Wet Sheet", 5, ItemShape.Box,
            new Vector3(0.32f, 0.02f, 0.22f), new Color(0.65f, 0.8f, 0.65f)),
        [ItemType.DrySheet] = new(ItemType.DrySheet, "Dry Sheet", 5, ItemShape.Box,
            new Vector3(0.32f, 0.02f, 0.22f), new Color(0.35f, 0.55f, 0.35f)),
        // A banded bundle of bills
        [ItemType.LooseBills] = new(ItemType.LooseBills, "Loose Bills", 20, ItemShape.Box,
            new Vector3(0.16f, 0.03f, 0.07f), new Color(0.3f, 0.5f, 0.3f)),
        // A cardboard box full of counterfeit cash
        [ItemType.CounterfeitBrick] = new(ItemType.CounterfeitBrick, "Counterfeit Brick", 4, ItemShape.Box,
            new Vector3(0.36f, 0.2f, 0.26f), new Color(0.6f, 0.45f, 0.28f)),
    };

    public static ItemDef Get(ItemType type) => Defs[type];
}
