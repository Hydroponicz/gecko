using Gecko.Core;
using Gecko.Production;
using Gecko.UI;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// Lets the player carry items (one type at a time) between the Stash and machines.
/// Tap interact to move one item; hold to keep moving them.
/// Child of the Player scene; needs a "Model/CarryAnchor" node on the player.
/// </summary>
public partial class PlayerInteraction : Node
{
    private const float MaxReach = 1.6f; // metres beyond the object's own radius
    private const float RepeatDelay = 0.3f;
    private const float RepeatInterval = 0.08f;

    private Player _player = null!;
    private BuildMode _buildMode = null!;
    private Node3D _carryAnchor = null!;

    private IInteractable? _target;
    private IInteractable? _heldTarget;
    private InteractMode _heldMode;
    private float _repeatTimer;

    public CarryState Carry { get; } = new();

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
            _target = null;
            _heldTarget = null;
            return;
        }

        // Keep the held target while the button is down so holding doesn't jump between objects.
        if (_heldTarget != null && Input.IsActionPressed(Actions.Interact))
        {
            _repeatTimer -= (float)delta;
            if (_repeatTimer <= 0f)
            {
                _repeatTimer = RepeatInterval;
                if (!_heldTarget.Interact(Carry, _heldMode))
                    _heldTarget = null;
            }
        }
        else
        {
            _heldTarget = null;
        }

        _target = FindTarget();
        if (_heldTarget == null && _target != null && Input.IsActionJustPressed(Actions.Interact))
        {
            var mode = _target.GetMode(Carry);
            if (mode is { } m && _target.Interact(Carry, m))
            {
                _heldTarget = _target;
                _heldMode = m;
                _repeatTimer = RepeatDelay;
            }
        }

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        string carrying = Carry.Type is { } type ? $"Carrying: {Items.Get(type).Name} x{Carry.Count}" : "";
        string action = "";
        var shown = _heldTarget ?? _target;
        if (shown != null)
        {
            var mode = _heldTarget != null ? _heldMode : shown.GetMode(Carry);
            if (mode is { } m)
                action = Prompts.For(Actions.Interact, shown.GetPrompt(m, Carry));
        }

        string hint = "";
        if (_buildMode.CanEnter())
            hint = Prompts.For(Actions.BuildToggle, "Build mode");

        GameHud.Instance.SetPrompt(string.Join("\n", new[] { carrying, action, hint }).Trim('\n'));
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
        var playerPos = _player.GlobalPosition;

        IInteractable? best = null;
        float bestScore = float.MinValue;
        foreach (var node in GetTree().GetNodesInGroup(FacilityNode.Group))
        {
            if (node is not FacilityNode facility)
                continue;
            foreach (var candidate in facility.Interactables())
            {
                var offset = candidate.InteractPosition - playerPos;
                offset.Y = 0f;
                float distance = offset.Length() - candidate.InteractRadius;
                if (distance > MaxReach)
                    continue;
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
