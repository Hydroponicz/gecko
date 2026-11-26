using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gecko.Production;

/// <summary>
/// A production property in the world: a walled room with a door, a build grid, the Stash,
/// and views for every machine. Owns the <see cref="FacilityState"/> and ticks it.
/// The local origin is the grid's corner; cells run along +X and +Z. The door is on the +Z wall.
/// </summary>
public partial class FacilityNode : Node3D
{
    public const string Group = "facilities";
    public const float CellSize = 1f;
    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const int DoorWidth = 2;
    private const int StashSlots = 4;

    [Export] public Vector2I SizeCells { get; set; } = new(14, 10);
    [Export] public string DisplayName { get; set; } = "Counterfeit Factory";

    private readonly Dictionary<MachineState, MachineView> _views = new();
    private readonly List<StashSlotView> _stashViews = [];
    private CashSpotView _cashSpot = null!;
    private readonly Dictionary<WorkerState, WorkerView> _workerViews = new();
    private Node3D _grid = null!;

    public FacilityState State { get; private set; } = null!;

    public bool GridVisible
    {
        get => _grid.Visible;
        set => _grid.Visible = value;
    }

    public override void _Ready()
    {
        AddToGroup(Group);

        // Stash: a row of shelves plus a cash table just inside the door, on its left.
        int doorStart = SizeCells.X / 2 - DoorWidth / 2;
        int stashWidth = StashSlots + 1;
        var stashArea = new Rect2I(Mathf.Max(0, doorStart - stashWidth), SizeCells.Y - 1, stashWidth, 1);
        var doorArea = new Rect2I(doorStart, SizeCells.Y - 1, DoorWidth, 1);
        State = new FacilityState(SizeCells, stashArea, StashSlots, doorArea);

        // Test supplies until buying supplies exists (roadmap step 3).
        State.Stash.Add(ItemType.Paper, 30);
        State.Stash.Add(ItemType.Ink, 30);

        BuildFloor();
        BuildWalls(doorStart);
        BuildGrid();
        BuildStash(stashArea);

        State.MachineAdded += OnMachineAdded;
        State.MachineRemoved += OnMachineRemoved;
        State.MachineMoved += machine => _views[machine].SyncTransform();
        State.WorkerAdded += OnWorkerAdded;
        State.WorkerRemoved += OnWorkerRemoved;
        Core.ClockNode.Clock.Payday += OnPayday;
    }

    public override void _ExitTree() => Core.ClockNode.Clock.Payday -= OnPayday;

    private void OnPayday()
    {
        int before = State.Workers.Count(w => w.Phase != WorkerPhase.Leaving);
        int paid = State.PayWages();
        int quit = before - State.Workers.Count(w => w.Phase != WorkerPhase.Leaving);
        if (before == 0)
            return;
        string text = $"Payday at {DisplayName}: {Economy.Money.Format(paid)} in wages";
        if (quit > 0)
            text += $"\n{quit} worker(s) quit: not enough cash on the cash table";
        UI.GameHud.Instance.Notify(text);
    }

    private void OnWorkerAdded(WorkerState worker)
    {
        var view = new WorkerView { Name = $"Worker{worker.Id}" };
        AddChild(view);
        view.Setup(this, worker);
        _workerViews[worker] = view;
    }

    private void OnWorkerRemoved(WorkerState worker)
    {
        if (_workerViews.Remove(worker, out var view))
            view.QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Workers steer around the player.
        Vector2? playerPosition = null;
        if (GetTree().GetFirstNodeInGroup(Actors.Player.Group) is Node3D player && ContainsPoint(player.GlobalPosition))
            playerPosition = WorldToGrid(player.GlobalPosition);
        State.SetPlayerPosition(playerPosition);
        State.Tick((float)delta);
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        var local = ToLocal(worldPosition);
        return local.X >= 0f && local.Z >= 0f && local.X <= SizeCells.X * CellSize && local.Z <= SizeCells.Y * CellSize;
    }

    /// <summary>Converts a world position to local grid coordinates (fractional cells).</summary>
    public Vector2 WorldToGrid(Vector3 worldPosition)
    {
        var local = ToLocal(worldPosition);
        return new Vector2(local.X / CellSize, local.Z / CellSize);
    }

    public Transform3D MachineLocalTransform(MachineDef def, Vector2I cell, int rotation)
    {
        var footprint = MachineState.RotateFootprint(def.Footprint, rotation);
        var center = new Vector3(cell.X + footprint.X / 2f, 0f, cell.Y + footprint.Y / 2f) * CellSize;
        return new Transform3D(new Basis(Vector3.Up, rotation * Mathf.Pi / 2f), center);
    }

    /// <summary>
    /// The closest spot next to a block of cells, as seen from <paramref name="fromWorld"/>:
    /// where a character stands to use a machine or shelf.
    /// </summary>
    public Vector3 AccessPoint(Rect2I cells, Vector3 fromWorld, float margin = 0.45f)
    {
        var local = ToLocal(fromWorld);
        float minX = cells.Position.X * CellSize - margin;
        float maxX = cells.End.X * CellSize + margin;
        float minZ = cells.Position.Y * CellSize - margin;
        float maxZ = cells.End.Y * CellSize + margin;
        var point = new Vector3(Mathf.Clamp(local.X, minX, maxX), 0f, Mathf.Clamp(local.Z, minZ, maxZ));
        var world = ToGlobal(point);
        world.Y = fromWorld.Y;
        return world;
    }

    public MachineView? ViewFor(MachineState machine) => _views.GetValueOrDefault(machine);

    public IEnumerable<IInteractable> Interactables()
    {
        foreach (var view in _views.Values)
        {
            if (view.Visible)
                yield return view;
        }
        foreach (var view in _stashViews)
            yield return view;
        yield return _cashSpot;
        foreach (var view in _workerViews.Values)
        {
            if (view.Visible)
                yield return view;
        }
    }

    private void OnMachineAdded(MachineState machine)
    {
        var view = new MachineView { Name = machine.Def.Id };
        AddChild(view);
        view.Setup(this, machine);
        _views[machine] = view;
    }

    private void OnMachineRemoved(MachineState machine)
    {
        if (_views.Remove(machine, out var view))
            view.QueueFree();
    }

    private void BuildFloor()
    {
        var size = new Vector2(SizeCells.X, SizeCells.Y) * CellSize;
        AddChild(new MeshInstance3D
        {
            Name = "Floor",
            Mesh = new PlaneMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.44f, 0.4f) },
            Position = new Vector3(size.X / 2f, 0.01f, size.Y / 2f),
        });
    }

    private void BuildWalls(int doorStart)
    {
        float width = SizeCells.X * CellSize;
        float depth = SizeCells.Y * CellSize;
        float t = WallThickness;

        AddWall(new Vector3(width / 2f, 0f, -t / 2f), new Vector3(width + 2f * t, WallHeight, t)); // back
        AddWall(new Vector3(-t / 2f, 0f, depth / 2f), new Vector3(t, WallHeight, depth)); // left
        AddWall(new Vector3(width + t / 2f, 0f, depth / 2f), new Vector3(t, WallHeight, depth)); // right

        // Front wall with a door gap.
        float doorLeft = doorStart * CellSize;
        float doorRight = (doorStart + DoorWidth) * CellSize;
        AddWall(new Vector3((doorLeft - t) / 2f, 0f, depth + t / 2f), new Vector3(doorLeft + t, WallHeight, t));
        float rightLength = width - doorRight + t;
        AddWall(new Vector3(doorRight + rightLength / 2f, 0f, depth + t / 2f), new Vector3(rightLength, WallHeight, t));
    }

    private void AddWall(Vector3 basePosition, Vector3 size)
    {
        var wall = new StaticBody3D { Position = basePosition + Vector3.Up * size.Y / 2f };
        wall.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.35f, 0.28f) }, // brick-ish
        });
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(wall);
    }

    private void BuildGrid()
    {
        _grid = new Node3D { Name = "Grid", Visible = false };
        AddChild(_grid);
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        float width = SizeCells.X * CellSize;
        float depth = SizeCells.Y * CellSize;
        const float line = 0.03f;
        for (int x = 0; x <= SizeCells.X; x++)
        {
            _grid.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(line, 0.005f, depth) },
                MaterialOverride = material,
                Position = new Vector3(x * CellSize, 0.02f, depth / 2f),
            });
        }
        for (int z = 0; z <= SizeCells.Y; z++)
        {
            _grid.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, 0.005f, line) },
                MaterialOverride = material,
                Position = new Vector3(width / 2f, 0.02f, z * CellSize),
            });
        }
    }

    private void BuildStash(Rect2I area)
    {
        for (int i = 0; i < StashSlots; i++)
        {
            var slot = new StashSlotView
            {
                Name = $"StashSlot{i}",
                Position = new Vector3(area.Position.X + i + 0.5f, 0f, area.Position.Y + 0.5f) * CellSize,
            };
            AddChild(slot);
            slot.Setup(this, i, new Vector2I(area.Position.X + i, area.Position.Y));
            _stashViews.Add(slot);
        }
        var cashCell = new Vector2I(area.Position.X + StashSlots, area.Position.Y);
        _cashSpot = new CashSpotView
        {
            Name = "CashTable",
            Position = new Vector3(cashCell.X + 0.5f, 0f, cashCell.Y + 0.5f) * CellSize,
        };
        AddChild(_cashSpot);
        _cashSpot.Setup(this, cashCell);
    }
}
