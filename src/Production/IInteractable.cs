using System;
using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>
/// One choice in the targeting menu, e.g. "Load Ink".
/// Disabled options are still shown (greyed out) with <see cref="Detail"/> explaining why,
/// so players learn what each object needs.
/// </summary>
/// <param name="Execute">Moves up to the given number of items; returns how many moved.</param>
public sealed record InteractOption(string Label, bool Enabled, string Detail, Func<int, int> Execute);

/// <summary>Something the player can target (hold L2 / LT / right mouse) and pick options for.</summary>
public interface IInteractable
{
    /// <summary>World-space point used for targeting.</summary>
    Vector3 InteractPosition { get; }

    /// <summary>Roughly how far the object extends from <see cref="InteractPosition"/>.</summary>
    float InteractRadius { get; }

    /// <summary>Where a character should stand to use this, given where they are now (world space).</summary>
    Vector3 GetAccessPoint(Vector3 from);

    /// <summary>False once the object is gone (e.g. a machine was removed).</summary>
    bool IsAvailable { get; }

    /// <summary>Menu title, e.g. "Printing Press".</summary>
    string Title { get; }

    /// <summary>One line of state under the title, e.g. "Paper 2/5  Ink 0/5".</summary>
    string Status { get; }

    /// <summary>Up to 4 options, in button order.</summary>
    IReadOnlyList<InteractOption> GetOptions(CarryState carry);

    void SetHighlighted(bool highlighted);
}
