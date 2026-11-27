using System.Collections.Generic;
using Gecko.Production;
using Gecko.World;
using Godot;

namespace Gecko.Vehicles;

/// <summary>Lets you target a flipped car and push it back onto its wheels. Only available while it's flipped.</summary>
public partial class FlippedCarView : Node3D, IInteractable
{
    private readonly VehicleNode _vehicle;

    public FlippedCarView(VehicleNode vehicle) => _vehicle = vehicle;
    public FlippedCarView() => _vehicle = null!;

    public Vector3 InteractPosition => _vehicle.GlobalPosition + Vector3.Up * 0.8f;
    public float InteractRadius => 2.3f;
    public bool IsAvailable => IsInstanceValid(this) && _vehicle.IsFlipped && _vehicle.Driver == null
                               && _vehicle.LinearVelocity.Length() < 1f;
    public string Title => $"{_vehicle.DisplayName} (flipped)";
    public string Status => "It's on its side. Give it a shove.";

    public override void _Ready() => AddToGroup(PayphoneNode.InteractablesGroup);

    /// <summary>Stand beside the car, on the side it should roll away from.</summary>
    public Vector3 GetAccessPoint(Vector3 from)
    {
        var side = _vehicle.GlobalBasis.X;
        side.Y = 0f;
        side = side.LengthSquared() > 0.01f ? side.Normalized() : Vector3.Right;
        var a = _vehicle.GlobalPosition + side * 2.0f;
        var b = _vehicle.GlobalPosition - side * 2.0f;
        var point = a.DistanceTo(from) < b.DistanceTo(from) ? a : b;
        point.Y = from.Y;
        return point;
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who) =>
    [
        new InteractOption("Push it back over", true, "takes a moment", _ =>
        {
            _vehicle.PushUpright();
            return 1;
        }),
    ];

    /// <summary>Takes a real shove: the player is busy for a couple of seconds.</summary>
    public float? ActionSeconds => 2.2f;

    public void SetHighlighted(bool highlighted) { }
}
