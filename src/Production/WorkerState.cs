using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

public enum WorkerPhase
{
    Arriving, // on the way to the property, not inside yet
    Idle,
    ToSource,
    Taking,
    ToDest,
    Giving,
    Waiting, // at the destination, which is full
    Leaving,
}

/// <summary>
/// A place items move between: a specific machine, or the Stash (Machine == null).
/// </summary>
public readonly record struct Endpoint(MachineState? Machine)
{
    public static readonly Endpoint Stash = new(null);
    public bool IsStash => Machine == null;
    public string Name => Machine?.Def.Name ?? "Stash";
}

/// <summary>
/// A job the layout needs: move items from any of <see cref="Sources"/> (or the Stash) into <see cref="Dest"/>.
/// Worked out automatically from the placed machines.
/// </summary>
public sealed record JobLink(IReadOnlyList<MachineState>? Sources, Endpoint Dest, IReadOnlyList<ItemType> Types)
{
    public string Name => $"{(Sources == null ? "Stash" : Sources[0].Def.Name)} -> {Dest.Name}";
}

/// <summary>Runtime state of one hired worker. Plain data, positions in grid units (cell centres at +0.5).</summary>
public sealed class WorkerState(int id, Vector2 position, float arriveDelay)
{
    public int Id { get; } = id;
    public Vector2 Position { get; internal set; } = position;
    public Vector2 Facing { get; internal set; } = Vector2.Up;
    public CarryState Carry { get; } = new();
    public WorkerPhase Phase { get; internal set; } = WorkerPhase.Arriving;
    public string Status { get; internal set; } = "On the way";

    /// <summary>0..1 progress of the current take/give action (for the lean pose).</summary>
    public float ActionProgress { get; internal set; }

    /// <summary>How many times the fail-safe had to teleport this worker (for debugging).</summary>
    public int Teleports { get; internal set; }

    public Vector2I Cell => new(Mathf.FloorToInt(Position.X), Mathf.FloorToInt(Position.Y));
    public bool IsActive => Phase != WorkerPhase.Arriving;

    // Task
    internal float ArriveTimer = arriveDelay;
    internal Endpoint Source;
    internal Endpoint Dest;
    internal ItemType TaskType;
    internal int TaskAmount;

    // Movement + fail-safes
    internal List<Vector2I> Path = [];
    internal float BlockedTimer;   // waiting for someone to move out of the next cell
    internal bool ReplannedAroundAgents;
    internal float NoProgressTimer; // time since last reaching a new cell while trying to move
    internal float RetryTimer;     // back-off after "can't reach"
    internal float WaitTimer;      // waiting at a full destination
    internal float ActionTimer;
}
