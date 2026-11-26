using Godot;

namespace Gecko.Production;

/// <summary>Whether an interaction puts items in (Give) or takes them out (Take).</summary>
public enum InteractMode
{
    Give,
    Take,
}

/// <summary>Something in a facility the player can move items into or out of.</summary>
public interface IInteractable
{
    /// <summary>World-space point used for targeting.</summary>
    Vector3 InteractPosition { get; }

    /// <summary>Roughly how far the object extends from <see cref="InteractPosition"/>.</summary>
    float InteractRadius { get; }

    /// <summary>What pressing interact would do right now, or null if nothing.
    /// The mode is locked while the button is held, so holding never flips between give and take.</summary>
    InteractMode? GetMode(CarryState carry);

    /// <summary>Prompt text for a mode, e.g. "Load Paper".</summary>
    string GetPrompt(InteractMode mode, CarryState carry);

    /// <summary>Moves one item in the given mode. Returns true if anything moved.</summary>
    bool Interact(CarryState carry, InteractMode mode);
}
