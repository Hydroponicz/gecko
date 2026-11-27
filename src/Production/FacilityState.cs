using System;
using System.Collections.Generic;
using Gecko.Economy;
using Godot;

namespace Gecko.Production;

/// <summary>
/// All production state for one property: the grid, placed machines and the Stash.
/// Plain data, no visuals. Every change goes through a method here (an "action"),
/// so co-op can later send the same actions over the network.
/// </summary>
public sealed partial class FacilityState
{
    private readonly List<MachineState> _machines = [];
    private readonly List<Delivery> _deliveries = [];

    /// <summary>A supply order on its way to the Stash.</summary>
    public sealed class Delivery(ItemType type, int amount, float seconds)
    {
        public ItemType Type { get; } = type;
        public int Amount { get; internal set; } = amount;
        public float Remaining { get; internal set; } = seconds;
    }

    public FacilityState(Vector2I size, Rect2I stashArea, int stashSlots, Rect2I doorArea)
    {
        Size = size;
        StashArea = stashArea;
        DoorArea = doorArea;
        Stash = new StashState(stashSlots);
    }

    public Vector2I Size { get; }
    public Rect2I StashArea { get; }

    /// <summary>Inside the doorway: kept clear so people can always get in and out.</summary>
    public Rect2I DoorArea { get; }

    /// <summary>Drive-in loading bay (warehouses), kept clear of machines. Null if the property has none.</summary>
    public Rect2I? LoadingBay { get; set; }
    public StashState Stash { get; }
    public IReadOnlyList<MachineState> Machines => _machines;
    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    public event Action<MachineState>? MachineAdded;
    public event Action<MachineState>? MachineRemoved;
    public event Action<MachineState>? MachineMoved;

    public void Tick(float delta)
    {
        foreach (var machine in _machines)
            machine.Tick(delta, Stash);
        TickWorkers(delta);

        for (int i = _deliveries.Count - 1; i >= 0; i--)
        {
            var delivery = _deliveries[i];
            delivery.Remaining -= delta;
            if (delivery.Remaining > 0f)
                continue;
            // If the Stash is full, the rest waits and is retried.
            delivery.Amount -= Stash.Add(delivery.Type, delivery.Amount);
            if (delivery.Amount <= 0)
                _deliveries.RemoveAt(i);
        }
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
        if (rect.Intersects(StashArea) || rect.Intersects(DoorArea))
            return false;
        if (LoadingBay is { } bay && rect.Intersects(bay))
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

    // ---- Supply actions ----

    /// <summary>Pays from the wallet and schedules a delivery to the Stash. False if you can't afford it.</summary>
    public bool OrderSupplies(Wallet wallet, IReadOnlyList<ItemType> types)
    {
        if (!wallet.SpendDirty(Supplier.OrderCost(types)))
            return false;
        foreach (var type in types)
            _deliveries.Add(new Delivery(type, Supplier.OrderSize, Supplier.DeliverySeconds));
        return true;
    }

    // ---- Cash actions. "units" = 1 for a tap, int.MaxValue for a hold. Each returns dollars moved. ----

    /// <summary>Wallet -> Stash, $1,000 per unit.</summary>
    public int DepositCash(Wallet wallet, int units)
    {
        int moved = wallet.TakeDirty((int)Math.Min((long)units * Money.StackValue, wallet.Dirty));
        Stash.DirtyCash += moved;
        return moved;
    }

    /// <summary>Stash -> wallet, $1,000 per unit, up to the wallet cap.</summary>
    public int WithdrawCash(Wallet wallet, int units)
    {
        long wanted = Math.Min((long)units * Money.StackValue, Stash.DirtyCash);
        int moved = wallet.AddDirty((int)wanted);
        Stash.DirtyCash -= moved;
        return moved;
    }

    /// <summary>Stash -> duffel bags you carry, up to one full bag per unit.</summary>
    public int PackBags(CarryState carry, int units)
    {
        long wanted = Math.Min((long)units * Money.BagCapacity, Stash.DirtyCash);
        int moved = carry.AddCashToBags((int)wanted);
        Stash.DirtyCash -= moved;
        return moved;
    }

    /// <summary>Duffel bags you carry -> Stash, one bag per unit.</summary>
    public int UnpackBags(CarryState carry, int units)
    {
        int moved = 0;
        for (int i = 0; i < units && carry.Type == ItemType.DuffelBag; i++)
            moved += carry.TakeBag();
        Stash.DirtyCash += moved;
        return moved;
    }
}
