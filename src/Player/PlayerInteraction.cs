using System.Collections.Generic;
using Gecko.Core;
using Gecko.Economy;
using Gecko.Production;
using Gecko.UI;
using Gecko.Vehicles;
using Gecko.World;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// RDR2-style interaction: hold Target (L2 / LT / right mouse) to lock onto what the camera
/// points at, then pick an option from the menu. Tap an option button to move one item,
/// hold it to move as many as possible. Carrying is one item type at a time.
/// Child of the Player scene; needs a "Model/CarryAnchor" node on the player.
/// </summary>
public partial class PlayerInteraction : Node
{
    private const float MaxReach = 1.6f; // metres from the spot where you would stand to use it
    private const float LoseReach = 2.5f; // a locked target is dropped beyond this
    private const float HoldTime = 0.45f;

    private Player _player = null!;
    private BuildMode _buildMode = null!;
    private Node3D _carryAnchor = null!;
    private Node3D _bagAnchor = null!;
    private Interactor _who = null!;

    private const float WalkTimeout = 3f;
    private const float ArriveDistance = 0.15f;

    private IInteractable? _target; // locked while Target is held
    private ActionTask? _task; // the walk-over-and-do-it action in progress

    /// <summary>An option the player chose: walk to the object, then perform it.</summary>
    private sealed class ActionTask(IInteractable target, string optionLabel, int amount)
    {
        public IInteractable Target { get; } = target;
        public string OptionLabel { get; } = optionLabel;
        public int Amount { get; } = amount;
        public bool Walking { get; set; } = true;
        public float Timer { get; set; }
        public float Duration { get; set; }
        public bool Applied { get; set; }
    }
    private int _pressedSlot = -1;
    private float _pressedTime;
    private bool _holdDone;

    public CarryState Carry { get; } = new();

    /// <summary>True while the player holds Target on something. Option buttons are taken then.</summary>
    public bool IsTargeting => _target != null;

    /// <summary>True while the character is walking to an object or performing an action on it.</summary>
    public bool IsBusy => _task != null;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _buildMode = _player.GetNode<BuildMode>("BuildMode");
        _carryAnchor = _player.GetNode<Node3D>("Model/CarryAnchor");
        _bagAnchor = _player.GetNode<Node3D>("Model/BagAnchor");
        _who = new Interactor(Carry, _player.Wallet);
        Carry.Changed += UpdateCarryVisuals;
        _player.Wallet.Changed += (dirty, clean) => ShowMoney(dirty + clean);
    }

    /// <summary>Items are carried in front. Duffel bags: 1st slung on the back, 2nd and 3rd in each hand.</summary>
    private void UpdateCarryVisuals()
    {
        bool bags = Carry.Type == ItemType.DuffelBag;
        ItemVisuals.BuildStack(_carryAnchor, bags ? null : Carry.Type, Carry.Count);

        foreach (var child in _bagAnchor.GetChildren())
            child.QueueFree();
        if (!bags)
            return;
        // Positions are relative to BagAnchor (just behind the player at waist height).
        for (int i = 0; i < Carry.Count; i++)
        {
            var bag = ItemVisuals.Create(ItemType.DuffelBag);
            switch (i)
            {
                case 0: // on the back, slung diagonally
                    bag.Position = new Vector3(0f, 0.35f, 0.08f);
                    bag.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(-30f));
                    _bagAnchor.AddChild(MakeSling());
                    break;
                case 1: // right hand, hanging at the side
                    bag.Position = new Vector3(0.5f, -0.4f, -0.42f);
                    bag.Rotation = new Vector3(0f, Mathf.Pi / 2f, 0f);
                    break;
                default: // left hand
                    bag.Position = new Vector3(-0.5f, -0.4f, -0.42f);
                    bag.Rotation = new Vector3(0f, Mathf.Pi / 2f, 0f);
                    break;
            }
            _bagAnchor.AddChild(bag);
        }
    }

    /// <summary>The strap across the chest holding the back bag.</summary>
    private static MeshInstance3D MakeSling() => new()
    {
        Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.95f, 0.03f) },
        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f) },
        // Runs from the right shoulder to the left hip, across the front of the body.
        Position = new Vector3(0f, 0.35f, -0.78f),
        Rotation = new Vector3(0f, 0f, Mathf.DegToRad(35f)),
    };

    /// <summary>Shows balances for a few seconds (on change, or when the player asks).</summary>
    private void ShowMoney(int change = 0)
    {
        var lines = new List<string>
        {
            ClockNode.Clock.TimeText,
            Money.Format(_player.Wallet.Dirty),
            $"Bank  {Money.Format(_player.Wallet.Clean)}",
        };
        if (Carry.Type == ItemType.DuffelBag)
            lines.Add($"Bags  {Money.Format(Carry.TotalBagCash)}");
        if (change != 0)
            lines.Add(change > 0 ? $"+{Money.Format(change)}" : Money.Format(change));
        GameHud.Instance.ShowMoney(string.Join("\n", lines));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed(Actions.ShowBalance))
            ShowMoney();

        if (_buildMode.Active && _task == null)
        {
            _player.LookAtTarget = null;
            SetTarget(null);
            GameHud.Instance.SetMenu(null);
            return;
        }

        if (_walkingToCar != null)
        {
            UpdateWalkToCar((float)delta);
            GameHud.Instance.SetPrompt($"Getting in the {_walkingToCar?.DisplayName}...");
            return;
        }

        if (_task != null)
        {
            _player.LookAtTarget = _task.Target.IsAvailable ? _task.Target.InteractPosition : null;
            UpdateTask((float)delta);
            return;
        }

        UpdateTarget();
        // RDR2-style: the camera locks onto (and follows) whatever you're targeting.
        var lookAt = _target?.InteractPosition;
        if (_player.Vehicle is { } car)
            car.LookAtTarget = lookAt;
        else
            _player.LookAtTarget = lookAt;
        if (_target != null)
            HandleOptions((float)delta);
        else
            HandleVehicleEnterExit();
        UpdateHud();
    }

    /// <summary>Triangle / F: get into a nearby car, or out of the one you're driving.</summary>
    private void HandleVehicleEnterExit()
    {
        if (!Input.IsActionJustPressed(Actions.VehicleEnterExit))
            return;
        if (_player.Vehicle is { } car)
        {
            if (Mathf.Abs(car.ForwardSpeed) < 3f || car.IsFlipped)
                _player.ExitVehicle(); // crawl out of a flipped car too
        }
        else if (NearbyTrunk() is { } trunkCar)
        {
            trunkCar.SetTrunkOpen(!trunkCar.TrunkOpen); // at the back: open / close the trunk
        }
        else if (NearbyVehicle() is { } vehicle)
        {
            // GTA-style: walk to the driver's door yourself, then get in.
            _walkingToCar = vehicle;
            _walkToCarTimer = 0f;
            _player.IsLocked = true;
        }
    }

    private VehicleNode? _walkingToCar;
    private float _walkToCarTimer;
    private const float EnterVehicleRadius = 6f;

    /// <summary>Walking over to get in a car. Moving the stick cancels it; arriving (or getting close) gets in.</summary>
    private void UpdateWalkToCar(float delta)
    {
        var car = _walkingToCar!;
        _walkToCarTimer += delta;
        var move = Input.GetVector(Actions.MoveLeft, Actions.MoveRight, Actions.MoveForward, Actions.MoveBack);
        bool cancelled = move.Length() > 0.6f && _walkToCarTimer > 0.2f;
        if (cancelled || car.Driver != null || car.IsFlipped || !IsInstanceValid(car))
        {
            StopWalkToCar();
            return;
        }
        var door = car.DriverDoorPosition;
        _player.ScriptedMoveTarget = door;
        var offset = door - _player.GlobalPosition;
        offset.Y = 0f;
        bool arrived = offset.Length() < 0.45f;
        bool closeEnough = _walkToCarTimer > 4f && offset.Length() < 2.5f; // blocked near the door: just get in
        if (arrived || closeEnough)
        {
            StopWalkToCar();
            _player.EnterVehicle(car);
        }
        else if (_walkToCarTimer > 6f)
        {
            StopWalkToCar(); // couldn't get there
        }
    }

    private void StopWalkToCar()
    {
        _walkingToCar = null;
        _player.ScriptedMoveTarget = null;
        _player.IsLocked = false;
    }

    /// <summary>A car whose trunk you're standing behind (and nearer to than its driver's door).</summary>
    private VehicleNode? NearbyTrunk()
    {
        foreach (var node in GetTree().GetNodesInGroup(VehicleNode.Group))
        {
            if (node is not VehicleNode vehicle || vehicle == _player.Vehicle)
                continue;
            var behind = vehicle.GlobalTransform * new Vector3(0f, 0f, -2.9f);
            var offset = behind - _player.GlobalPosition;
            offset.Y = 0f;
            if (offset.Length() < 1.6f && offset.Length() < vehicle.DriverDoorPosition.DistanceTo(_player.GlobalPosition))
                return vehicle;
        }
        return null;
    }

    /// <summary>The nearest free, upright car within a few metres (you walk to its door to get in).</summary>
    private VehicleNode? NearbyVehicle()
    {
        VehicleNode? best = null;
        float bestDistance = EnterVehicleRadius;
        foreach (var node in GetTree().GetNodesInGroup(VehicleNode.Group))
        {
            if (node is not VehicleNode { Driver: null, IsFlipped: false } vehicle)
                continue;
            float distance = vehicle.GlobalPosition.DistanceTo(_player.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = vehicle;
            }
        }
        return best;
    }

    /// <summary>On foot you target with L2 / LT; in a vehicle L2 is the brake, so it's D-pad up.</summary>
    private StringName TargetAction => _player.Vehicle != null ? Actions.VehicleTarget : Actions.Target;

    private void UpdateTarget()
    {
        if (!Input.IsActionPressed(TargetAction))
        {
            SetTarget(null);
            return;
        }
        if (_target != null && (!_target.IsAvailable || DistanceTo(_target) > LoseReach))
            SetTarget(null);
        if (_target == null)
            SetTarget(FindTarget()); // keeps looking while held, until something is in reach
    }

    private void SetTarget(IInteractable? target)
    {
        if (target == _target)
            return;
        if (_target is { IsAvailable: true })
            _target.SetHighlighted(false);
        _target = target;
        _target?.SetHighlighted(true);
        _pressedSlot = -1;
    }

    /// <summary>Tap = 1 item (on release), hold = as many as possible (once the bar fills).</summary>
    private void HandleOptions(float delta)
    {
        for (int i = 0; i < Actions.Options.Length; i++)
        {
            if (Input.IsActionJustPressed(Actions.Options[i]))
            {
                _pressedSlot = i;
                _pressedTime = 0f;
                _holdDone = false;
            }
        }
        if (_pressedSlot < 0)
            return;

        var action = Actions.Options[_pressedSlot];
        if (Input.IsActionPressed(action))
        {
            _pressedTime += delta;
            if (!_holdDone && _pressedTime >= HoldTime)
            {
                _holdDone = true;
                Execute(_pressedSlot, int.MaxValue);
            }
            return;
        }

        // Released
        if (!_holdDone)
            Execute(_pressedSlot, 1);
        _pressedSlot = -1;
    }

    /// <summary>Starts the chosen option: the player is locked, walks over (objects only), then performs it.</summary>
    private void Execute(int slot, int amount)
    {
        if (_target == null)
            return;
        var options = _target.GetOptions(_who);
        if (slot >= options.Count || !options[slot].Enabled)
            return;

        var target = _target;
        SetTarget(null);
        target.SetHighlighted(true);
        _task = new ActionTask(target, options[slot].Label, amount);
        _player.IsLocked = true;
        if (target.RequiresApproach)
        {
            _player.ScriptedMoveTarget = target.GetAccessPoint(_player.GlobalPosition);
        }
        else
        {
            // People: no walking over, just a short gesture from where you stand.
            _player.ScriptedMoveTarget = null;
            _task.Walking = false;
            _task.Duration = 0.4f;
        }
        GameHud.Instance.SetMenu(null);
        GameHud.Instance.SetPrompt($"{options[slot].Label}...");
    }

    private void UpdateTask(float delta)
    {
        var task = _task!;
        if (!task.Target.IsAvailable)
        {
            EndTask();
            return;
        }
        task.Timer += delta;

        if (task.Walking)
        {
            var toSpot = _player.ScriptedMoveTarget!.Value - _player.GlobalPosition;
            toSpot.Y = 0f;
            bool arrived = toSpot.Length() < ArriveDistance;
            // Blocked on the way: if we're still close enough to reach it, just do it from here.
            bool stuck = task.Timer > WalkTimeout;
            if (stuck && DistanceTo(task.Target) > MaxReach)
            {
                EndTask();
                return;
            }
            if (arrived || stuck)
            {
                task.Walking = false;
                task.Timer = 0f;
                // Placeholder timing until real animations: longer when moving more items.
                int moving = Mathf.Min(task.Amount, 5);
                task.Duration = task.Target.ActionSeconds ?? 0.6f + 0.15f * (moving - 1);
                _player.ScriptedMoveTarget = null;
            }
            return;
        }

        // Performing: face the object, lean in, items move at the halfway point.
        _player.FaceTowards(task.Target.InteractPosition, delta);
        float t = Mathf.Clamp(task.Timer / task.Duration, 0f, 1f);
        _player.SetLean(Mathf.Sin(t * Mathf.Pi));
        if (!task.Applied && t >= 0.5f)
        {
            task.Applied = true;
            foreach (var option in task.Target.GetOptions(_who))
            {
                if (option.Label == task.OptionLabel && option.Enabled)
                {
                    if (option.Execute(task.Amount) > 0)
                        Gamepad.Instance.Rumble(0.15f, 0f, 0.05f);
                    break;
                }
            }
        }
        if (t >= 1f)
            EndTask();
    }

    private void EndTask()
    {
        if (_task is { Target.IsAvailable: true })
            _task.Target.SetHighlighted(false);
        _task = null;
        _player.SetLean(0f);
        _player.ScriptedMoveTarget = null;
        _player.IsLocked = false;
        _pressedSlot = -1;
    }

    private void UpdateHud()
    {
        string carrying = Carry.Type switch
        {
            null => "",
            ItemType.DuffelBag => $"Carrying: {Carry.Count} duffel bag(s), {Money.Format(Carry.TotalBagCash)}" +
                                  (Carry.IsEncumbered ? "  (weighed down)" : ""),
            { } type => $"Carrying: {Items.Get(type).Name} x{Carry.Count}",
        };

        if (_target != null)
        {
            var options = _target.GetOptions(_who);
            var rows = new GameHud.MenuRow[Mathf.Min(options.Count, GameHud.MenuRows)];
            for (int i = 0; i < rows.Length; i++)
            {
                var option = options[i];
                float hold = i == _pressedSlot && !_holdDone ? Mathf.Clamp(_pressedTime / HoldTime, 0f, 1f) : 0f;
                rows[i] = new GameHud.MenuRow(Prompts.ButtonName(Actions.Options[i]), option.Label, option.Detail,
                    option.Enabled, hold);
            }
            GameHud.Instance.SetMenu(_target.Title, _target.Status, rows, "Tap: 1   Hold: all");
            GameHud.Instance.SetPrompt(carrying);
            return;
        }

        GameHud.Instance.SetMenu(null);
        string hint = FindTarget() != null ? Prompts.For(TargetAction, "Interact") : "";
        string build = _buildMode.CanEnter() ? Prompts.For(Actions.BuildToggle, "Build mode") : "";
        string vehicle = _player.Vehicle is { } car
            ? car.IsFlipped ? Prompts.For(Actions.VehicleEnterExit, "Crawl out")
            : Mathf.Abs(car.ForwardSpeed) < 3f ? Prompts.For(Actions.VehicleEnterExit, "Get out") : ""
            : NearbyTrunk() is { } trunkCar
                ? Prompts.For(Actions.VehicleEnterExit, trunkCar.TrunkOpen ? "Close trunk" : "Open trunk")
                : NearbyVehicle() is { } parked ? Prompts.For(Actions.VehicleEnterExit, $"Get in the {parked.DisplayName}") : "";
        // Temporary speedometer while tuning car handling.
        string speed = _player.Vehicle is { } driving ? $"{Mathf.RoundToInt(Mathf.Abs(driving.ForwardSpeed) * 3.6f)} km/h" : "";
        GameHud.Instance.SetPrompt(string.Join("\n", new[] { speed, carrying, hint, vehicle, build }).Trim('\n'));
    }

    /// <summary>
    /// Distance from the player to the object's nearest edge (its access spot), so long machines
    /// count as close when you stand by any side. Ties go to the nearer centre.
    /// </summary>
    private float DistanceTo(IInteractable target)
    {
        var pos = _player.GlobalPosition;
        var toEdge = target.GetAccessPoint(pos) - pos;
        toEdge.Y = 0f;
        var toCenter = target.InteractPosition - pos;
        toCenter.Y = 0f;
        return toEdge.Length() + toCenter.Length() * 0.01f;
    }

    /// <summary>Picks the interactable closest to the player, within reach.</summary>
    private IInteractable? FindTarget()
    {
        IInteractable? best = null;
        float bestDistance = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup(FacilityNode.Group))
        {
            if (node is not FacilityNode facility)
                continue;
            foreach (var candidate in facility.Interactables())
                Consider(candidate);
        }
        foreach (var node in GetTree().GetNodesInGroup(PayphoneNode.InteractablesGroup))
        {
            if (node is IInteractable candidate)
                Consider(candidate);
        }
        return best;

        void Consider(IInteractable candidate)
        {
            // From a vehicle you can only deal with people (no walking over to things), with a bit more reach.
            bool inVehicle = _player.Vehicle != null;
            if (!candidate.IsAvailable || (inVehicle && candidate.RequiresApproach))
                return;
            float distance = DistanceTo(candidate);
            if (distance <= (inVehicle ? MaxReach + 2.5f : MaxReach) && distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
    }
}
