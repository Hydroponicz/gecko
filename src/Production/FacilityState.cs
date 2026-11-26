using System;
using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>
/// All production state for one property: the grid, placed machines and the Stash.
/// Plain data, no visuals. Every change goes through a method here (an "action"),
/// so co-op can later send the same actions over the network.
/// </summary>
public sealed class FacilityState
{
    private readonly List<MachineState> _machines = [];

    public FacilityState(Vector2I size, Rect2I stashArea, int stashSlots)
    {
        Size = size;
        StashArea = stashArea;
        Stash = new StashState(stashSlots);
    }

    public Vector2I Size { get; }
    public Rect2I StashArea { get; }
    public StashState Stash { get; }
    public IReadOnlyList<MachineState> Machines => _machines;

    public event Action<MachineState>? MachineAdded;
    public event Action<MachineState>? MachineRemoved;
    public event Action<MachineState>? MachineMoved;

    public void Tick(float delta)
    {
        foreach (var machine in _machines)
            machine.Tick(delta, Stash);
    }

    public MachineState? MachineAt(Vector2I cell)
    {
        foreach (var machine in _machines)
        {
            if (machine.Rect.HasPoint(cell))
                return machine;
        }
        return null;
    }

    /// <param name="ignore">A machine being moved, so it doesn't block itself.</param>
    /// <param name="blocked">Extra cells that can't be built on (e.g. where the player stands).</param>
    public bool CanPlace(MachineDef def, Vector2I cell, int rotation, MachineState? ignore = null, Rect2I? blocked = null)
    {
        var rect = new Rect2I(cell, MachineState.RotateFootprint(def.Footprint, rotation));
        if (rect.Position.X < 0 || rect.Position.Y < 0 || rect.End.X > Size.X || rect.End.Y > Size.Y)
            return false;
        if (rect.Intersects(StashArea))
            return false;
        if (blocked is { } b && rect.Intersects(b))
            return false;
        foreach (var machine in _machines)
        {
            if (machine != ignore && rect.Intersects(machine.Rect))
                return false;
        }
        return true;
    }

    // ---- Build actions ----

    public MachineState? PlaceMachine(MachineDef def, Vector2I cell, int rotation, Rect2I? blocked = null)
    {
        if (!CanPlace(def, cell, rotation, null, blocked))
            return null;
        var machine = new MachineState(def, cell, rotation);
        _machines.Add(machine);
        MachineAdded?.Invoke(machine);
        return machine;
    }

    public bool MoveMachine(MachineState machine, Vector2I cell, int rotation, Rect2I? blocked = null)
    {
        if (!_machines.Contains(machine) || !CanPlace(machine.Def, cell, rotation, machine, blocked))
            return false;
        machine.Cell = cell;
        machine.Rotation = rotation;
        MachineMoved?.Invoke(machine);
        return true;
    }

    /// <summary>Removes a machine. Items inside go to the Stash if there is room.</summary>
    public bool RemoveMachine(MachineState machine)
    {
        if (!_machines.Remove(machine))
            return false;
        foreach (var (type, count) in machine.Contents())
            Stash.Add(type, count);
        MachineRemoved?.Invoke(machine);
        return true;
    }

    // ---- Carry actions (each returns how many items moved) ----

    public int LoadMachine(MachineState machine, CarryState carry, int amount)
    {
        if (carry.Type is not { } type)
            return 0;
        int moved = machine.AddInput(type, Math.Min(amount, carry.Count));
        carry.Remove(moved);
        return moved;
    }

    public int TakeFromMachine(MachineState machine, CarryState carry, int amount)
    {
        int moved = machine.TakeOutput(Math.Min(amount, carry.RoomFor(machine.Def.Output)));
        carry.Add(machine.Def.Output, moved);
        return moved;
    }

    public int StoreInStash(CarryState carry, int amount)
    {
        if (carry.Type is not { } type)
            return 0;
        int moved = Stash.Add(type, Math.Min(amount, carry.Count));
        carry.Remove(moved);
        return moved;
    }

    public int TakeFromStash(int slotIndex, CarryState carry, int amount)
    {
        if (Stash.Slots[slotIndex].Type is not { } type)
            return 0;
        int moved = Stash.Take(slotIndex, Math.Min(amount, carry.RoomFor(type)));
        carry.Add(type, moved);
        return moved;
    }
}
