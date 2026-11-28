using System;
using System.Collections.Generic;
using Gecko.Economy;
using Godot;

namespace Gecko.Production;

/// <summary>Whoever is interacting (the player now, workers or co-op players later).</summary>
public sealed class Interactor(CarryState carry, Wallet wallet)
{
    public CarryState Carry { get; } = carry;
    public Wallet Wallet { get; } = wallet;
}

/// <summary>
/// One choice in the targeting menu, e.g. "Load Ink".
/// Disabled options are still shown (greyed out) with <see cref="Detail"/> explaining why,
/// so players learn what each object needs.
/// </summary>
/// <param name="Execute">Called with 1 for a tap or int.MaxValue for a hold; returns how much happened (0 = nothing).</param>
/// <param name="Submenu">If set, choosing this opens a sub-menu instead (e.g. phone: "Arrange a sale" -> contacts).</param>
public sealed record InteractOption(
    string Label, bool Enabled, string Detail, Func<int, int> Execute,
    Func<IReadOnlyList<InteractOption>>? Submenu = null)
{
    /// <summary>A menu entry that opens a sub-menu.</summary>
    public static InteractOption Menu(string label, string detail, Func<IReadOnlyList<InteractOption>> submenu, bool enabled = true) =>
        new(label, enabled, detail, _ => 0, submenu);
}

/// <summary>Something the player can target (hold L2 / LT / right mouse) and pick options for.</summary>
public interface IInteractable
{
    /// <summary>World-space point used for targeting.</summary>
    Vector3 InteractPosition { get; }

    /// <summary>Roughly how far the object extends from <see cref="InteractPosition"/>.</summary>
    float InteractRadius { get; }

    /// <summary>Where a character should stand to use this, given where they are now (world space).</summary>
    Vector3 GetAccessPoint(Vector3 from);

    /// <summary>
    /// True for objects you walk up to and handle (machines, shelves, phones).
    /// False for people: options apply from where you stand (like talking in RDR2).
    /// </summary>
    bool RequiresApproach => true;

    /// <summary>How long the action takes, if not the usual quick put-down (e.g. pushing a car over).</summary>
    float? ActionSeconds => null;

    /// <summary>False once the object is gone (e.g. a machine was removed).</summary>
    bool IsAvailable { get; }

    /// <summary>Menu title, e.g. "Printing Press".</summary>
    string Title { get; }

    /// <summary>One line of state under the title, e.g. "Paper 2/5  Ink 0/5".</summary>
    string Status { get; }

    /// <summary>Up to 4 options, in button order.</summary>
    IReadOnlyList<InteractOption> GetOptions(Interactor who);

    void SetHighlighted(bool highlighted);
}
