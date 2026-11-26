using System;
using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>Runtime state of one placed machine. Plain data, no visuals.</summary>
public sealed class MachineState
{
    private readonly Dictionary<ItemType, int> _inputs = new();
    private readonly List<float> _crafts = []; // remaining seconds per active craft

    public MachineState(MachineDef def, Vector2I cell, int rotation)
    {
        Def = def;
        Cell = cell;
        Rotation = rotation;
        foreach (var input in def.Inputs.Keys)
            _inputs[input] = 0;
    }

    public MachineDef Def { get; }
    public Vector2I Cell { get; internal set; }
    /// <summary>0-3, quarter turns.</summary>
    public int Rotation { get; internal set; }
    public int OutputCount { get; private set; }
    public int ActiveCrafts => _crafts.Count;

    public Vector2I RotatedFootprint => RotateFootprint(Def.Footprint, Rotation);
    public Rect2I Rect => new(Cell, RotatedFootprint);

    public static Vector2I RotateFootprint(Vector2I footprint, int rotation) =>
        rotation % 2 == 0 ? footprint : new Vector2I(footprint.Y, footprint.X);

    public bool Accepts(ItemType type) => _inputs.ContainsKey(type);
    /// <summary>Items waiting to be used.</summary>
    public int InputCount(ItemType type) => _inputs.GetValueOrDefault(type);

    /// <summary>Everything of this input inside the machine: waiting plus currently being worked on.
    /// This is what capacity counts, so "holds 5" means 5 in total.</summary>
    public int Loaded(ItemType type) =>
        InputCount(type) + ActiveCrafts * Def.Inputs.GetValueOrDefault(type);

    public int InputRoom(ItemType type) => Accepts(type) ? Mathf.Max(0, Def.InputCapacity - Loaded(type)) : 0;

    /// <summary>Progress 0..1 of the craft closest to finishing, or 0 if idle.</summary>
    public float LeadProgress
    {
        get
        {
            if (_crafts.Count == 0)
                return 0f;
            float minRemaining = float.MaxValue;
            foreach (float remaining in _crafts)
                minRemaining = Math.Min(minRemaining, remaining);
            return 1f - minRemaining / Def.Duration;
        }
    }

    internal int AddInput(ItemType type, int amount)
    {
        int added = Math.Min(amount, InputRoom(type));
        if (added > 0)
            _inputs[type] += added;
        return added;
    }

    internal int TakeOutput(int amount)
    {
        int taken = Math.Min(amount, OutputCount);
        OutputCount -= taken;
        return taken;
    }

    internal void Tick(float delta, StashState stash)
    {
        for (int i = _crafts.Count - 1; i >= 0; i--)
        {
            _crafts[i] -= delta;
            if (_crafts[i] > 0f)
                continue;
            _crafts.RemoveAt(i);
            int produced = Def.OutputAmount;
            if (Def.OutputToStash)
                produced -= stash.Add(Def.Output, produced);
            OutputCount += produced;
        }

        while (_crafts.Count < Def.Slots && CanStartCraft())
        {
            foreach (var (type, needed) in Def.Inputs)
                _inputs[type] -= needed;
            _crafts.Add(Def.Duration);
        }
    }

    /// <summary>Everything inside the machine (used when it is removed). Active crafts are lost.</summary>
    internal IEnumerable<(ItemType Type, int Count)> Contents()
    {
        foreach (var (type, count) in _inputs)
        {
            if (count > 0)
                yield return (type, count);
        }
        if (OutputCount > 0)
            yield return (Def.Output, OutputCount);
    }

    private bool CanStartCraft()
    {
        foreach (var (type, needed) in Def.Inputs)
        {
            if (_inputs[type] < needed)
                return false;
        }
        // Reserve output room for everything already in progress.
        return OutputCount + (_crafts.Count + 1) * Def.OutputAmount <= Def.OutputCapacity;
    }
}
