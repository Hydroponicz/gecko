using System.Collections.Generic;
using Gecko.Core;
using Gecko.Production;
using Gecko.UI;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// Placing, moving and removing machines. Only available while standing inside a facility.
/// Aim with the camera; a see-through preview snaps to the grid (green = fits, red = blocked).
/// Child of the Player scene.
/// </summary>
public partial class BuildMode : Node
{
    private const float MaxAimDistance = 9f;

    private static readonly Color ValidColor = new(0.2f, 1f, 0.3f, 0.4f);
    private static readonly Color InvalidColor = new(1f, 0.2f, 0.2f, 0.4f);

    private Player _player = null!;
    private MeshInstance3D _ghost = null!;
    private StandardMaterial3D _ghostMaterial = null!;

    private FacilityNode? _facility;
    private int _selected;
    private int _rotation;
    private MachineState? _lifted;
    private Vector2I _liftedFromCell;
    private int _liftedFromRotation;

    // Result of this frame's aim.
    private Vector2I? _aimCell;
    private MachineState? _aimedMachine;
    private bool _placementValid;

    public bool Active => _facility != null;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _ghostMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = ValidColor,
        };
        _ghost = new MeshInstance3D { TopLevel = true, Visible = false, MaterialOverride = _ghostMaterial };
        AddChild(_ghost);
    }

    /// <summary>True when the player stands in a facility and could open build mode.</summary>
    public bool CanEnter() => !Active && !_player.IsLocked && CurrentFacility() != null;

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed(Actions.BuildToggle))
        {
            if (Active)
                Exit();
            else if (!_player.IsLocked && CurrentFacility() is { } facility)
                Enter(facility);
        }

        if (_facility == null)
            return;
        if (!_facility.ContainsPoint(_player.GlobalPosition))
        {
            Exit(); // walked out of the building
            return;
        }

        HandleSelection();
        UpdateAim();
        HandleActions();
        UpdateGhost();
        UpdateHud();
    }

    private void Enter(FacilityNode facility)
    {
        _facility = facility;
        _facility.GridVisible = true;
    }

    private void Exit()
    {
        if (_facility == null)
            return;
        if (_lifted != null)
            DropLifted(_liftedFromCell, _liftedFromRotation); // put it back where it was
        _facility.GridVisible = false;
        _facility = null;
        _ghost.Visible = false;
        GameHud.Instance.SetBuildInfo("");
    }

    private FacilityNode? CurrentFacility()
    {
        foreach (var node in GetTree().GetNodesInGroup(FacilityNode.Group))
        {
            if (node is FacilityNode facility && facility.ContainsPoint(_player.GlobalPosition))
                return facility;
        }
        return null;
    }

    private MachineDef CurrentDef => _lifted?.Def ?? Machines.All[_selected];

    private void HandleSelection()
    {
        if (_lifted == null)
        {
            int count = Machines.All.Count;
            if (Input.IsActionJustPressed(Actions.BuildNext))
                _selected = (_selected + 1) % count;
            if (Input.IsActionJustPressed(Actions.BuildPrev))
                _selected = (_selected - 1 + count) % count;
        }
        if (Input.IsActionJustPressed(Actions.BuildRotate))
            _rotation = (_rotation + 1) % 4;
    }

    /// <summary>Casts from the screen centre to the facility floor and finds the cell under it.</summary>
    private void UpdateAim()
    {
        _aimCell = null;
        _aimedMachine = null;
        _placementValid = false;

        var camera = GetViewport().GetCamera3D();
        if (camera == null || _facility == null)
            return;
        var screenCenter = GetViewport().GetVisibleRect().Size / 2f;
        var origin = camera.ProjectRayOrigin(screenCenter);
        var direction = camera.ProjectRayNormal(screenCenter);
        float floorY = _facility.GlobalPosition.Y;
        if (direction.Y > -0.01f)
            return; // looking at or above the horizon
        var hit = origin + direction * ((floorY - origin.Y) / direction.Y);
        if (hit.DistanceTo(_player.GlobalPosition) > MaxAimDistance || !_facility.ContainsPoint(hit))
            return;

        var grid = _facility.WorldToGrid(hit);
        _aimedMachine = _facility.State.MachineAt(new Vector2I(Mathf.FloorToInt(grid.X), Mathf.FloorToInt(grid.Y)));

        // Centre the footprint on the aim point.
        var footprint = MachineState.RotateFootprint(CurrentDef.Footprint, _rotation);
        _aimCell = new Vector2I(
            Mathf.RoundToInt(grid.X - footprint.X / 2f),
            Mathf.RoundToInt(grid.Y - footprint.Y / 2f));
        _placementValid = _facility.State.CanPlace(CurrentDef, _aimCell.Value, _rotation, _lifted, PlayerCells());
    }

    private void HandleActions()
    {
        if (_facility == null)
            return;

        if (Input.IsActionJustPressed(Actions.BuildPlace) && _aimCell is { } cell && _placementValid)
        {
            if (_lifted != null)
                DropLifted(cell, _rotation);
            else
                _facility.State.PlaceMachine(CurrentDef, cell, _rotation, PlayerCells());
        }

        // Pick up a placed machine to move it.
        if (Input.IsActionJustPressed(Actions.BuildMove) && _lifted == null && _aimedMachine is { } toLift)
        {
            _lifted = toLift;
            _liftedFromCell = toLift.Cell;
            _liftedFromRotation = toLift.Rotation;
            _rotation = toLift.Rotation;
            _facility.ViewFor(toLift)?.SetLifted(true);
        }

        if (Input.IsActionJustPressed(Actions.BuildRemove))
        {
            var toRemove = _lifted ?? _aimedMachine;
            if (toRemove != null)
            {
                _lifted = null;
                _facility.State.RemoveMachine(toRemove);
            }
        }
    }

    private void DropLifted(Vector2I cell, int rotation)
    {
        if (_lifted == null || _facility == null)
            return;
        _facility.State.MoveMachine(_lifted, cell, rotation);
        _facility.ViewFor(_lifted)?.SetLifted(false);
        _lifted = null;
    }

    private void UpdateGhost()
    {
        if (_facility == null || _aimCell is not { } cell)
        {
            _ghost.Visible = false;
            return;
        }
        var def = CurrentDef;
        if (_ghost.Mesh is not BoxMesh box || box.Size != GhostSize(def))
            _ghost.Mesh = new BoxMesh { Size = GhostSize(def) };
        var local = _facility.MachineLocalTransform(def, cell, _rotation);
        local.Origin += Vector3.Up * def.Height / 2f;
        _ghost.GlobalTransform = _facility.GlobalTransform * local;
        _ghostMaterial.AlbedoColor = _placementValid ? ValidColor : InvalidColor;
        _ghost.Visible = true;
    }

    private static Vector3 GhostSize(MachineDef def) =>
        new(def.Footprint.X * 0.95f, def.Height, def.Footprint.Y * 0.95f);

    private void UpdateHud()
    {
        var lines = new List<string>();
        if (_lifted != null)
        {
            lines.Add($"BUILD MODE  -  Moving: {_lifted.Def.Name}");
            lines.Add($"{Prompts.For(Actions.BuildPlace, "Put down")}   {Prompts.For(Actions.BuildRotate, "Rotate")}   " +
                      $"{Prompts.For(Actions.BuildRemove, "Remove")}");
        }
        else
        {
            lines.Add($"BUILD MODE  -  {Prompts.For(Actions.BuildPrev)} {CurrentDef.Name} {Prompts.For(Actions.BuildNext)}");
            var actions = $"{Prompts.For(Actions.BuildPlace, "Place")}   {Prompts.For(Actions.BuildRotate, "Rotate")}";
            if (_aimedMachine != null)
                actions += $"   {Prompts.For(Actions.BuildMove, "Move")}   {Prompts.For(Actions.BuildRemove, "Remove")}";
            lines.Add(actions);
        }
        lines.Add(Prompts.For(Actions.BuildToggle, "Exit build mode"));
        GameHud.Instance.SetBuildInfo(string.Join("\n", lines));
    }

    /// <summary>The cell the player stands on, so machines can't be placed on top of them.</summary>
    private Rect2I? PlayerCells()
    {
        if (_facility == null)
            return null;
        var grid = _facility.WorldToGrid(_player.GlobalPosition);
        return new Rect2I(Mathf.FloorToInt(grid.X), Mathf.FloorToInt(grid.Y), 1, 1);
    }
}
