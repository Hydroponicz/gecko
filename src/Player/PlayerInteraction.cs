using Gecko.Core;
using Gecko.Production;
using Gecko.UI;
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
    private const float MaxReach = 1.6f; // metres beyond the object's own radius
    private const float LoseReach = 2.5f; // a locked target is dropped beyond this
    private const float HoldTime = 0.45f;

    private Player _player = null!;
    private BuildMode _buildMode = null!;
    private Node3D _carryAnchor = null!;

    private IInteractable? _target; // locked while Target is held
    private int _pressedSlot = -1;
    private float _pressedTime;
    private bool _holdDone;

    public CarryState Carry { get; } = new();

    /// <summary>True while the player holds Target on something. Option buttons are taken then.</summary>
    public bool IsTargeting => _target != null;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        _buildMode = _player.GetNode<BuildMode>("BuildMode");
        _carryAnchor = _player.GetNode<Node3D>("Model/CarryAnchor");
        Carry.Changed += () => ItemVisuals.BuildStack(_carryAnchor, Carry.Type, Carry.Count);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_buildMode.Active)
        {
            SetTarget(null);
            GameHud.Instance.SetMenu(null);
            return;
        }

        UpdateTarget();
        if (_target != null)
            HandleOptions((float)delta);
        UpdateHud();
    }

    private void UpdateTarget()
    {
        if (!Input.IsActionPressed(Actions.Target))
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

    private void Execute(int slot, int amount)
    {
        if (_target == null)
            return;
        var options = _target.GetOptions(Carry);
        if (slot >= options.Count || !options[slot].Enabled)
            return;
        if (options[slot].Execute(amount) > 0)
            Gamepad.Instance.Rumble(0.15f, 0f, 0.05f);
    }

    private void UpdateHud()
    {
        string carrying = Carry.Type is { } type ? $"Carrying: {Items.Get(type).Name} x{Carry.Count}" : "";

        if (_target != null)
        {
            var options = _target.GetOptions(Carry);
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
        string hint = FindTarget() != null ? Prompts.For(Actions.Target, "Interact") : "";
        string build = _buildMode.CanEnter() ? Prompts.For(Actions.BuildToggle, "Build mode") : "";
        GameHud.Instance.SetPrompt(string.Join("\n", new[] { carrying, hint, build }).Trim('\n'));
    }

    private float DistanceTo(IInteractable target)
    {
        var offset = target.InteractPosition - _player.GlobalPosition;
        offset.Y = 0f;
        return offset.Length() - target.InteractRadius;
    }

    /// <summary>Picks the interactable the camera is most pointed at, within reach.</summary>
    private IInteractable? FindTarget()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return null;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0f;
        forward = forward.Normalized();

        IInteractable? best = null;
        float bestScore = float.MinValue;
        foreach (var node in GetTree().GetNodesInGroup(FacilityNode.Group))
        {
            if (node is not FacilityNode facility)
                continue;
            foreach (var candidate in facility.Interactables())
            {
                float distance = DistanceTo(candidate);
                if (distance > MaxReach)
                    continue;
                var offset = candidate.InteractPosition - _player.GlobalPosition;
                offset.Y = 0f;
                float facing = offset.LengthSquared() > 0.0001f ? forward.Dot(offset.Normalized()) : 1f;
                if (facing < 0.3f && distance > 0.3f)
                    continue;
                float score = facing - distance * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }
        return best;
    }
}
