using System.Collections.Generic;
using Gecko.Production;
using Godot;

namespace Gecko.World;

/// <summary>A street pay phone. Calls the supplier; orders go to your property's Stash.</summary>
public partial class PayphoneNode : StaticBody3D, IInteractable
{
    /// <summary>Non-facility interactables join this group so targeting can find them.</summary>
    public const string InteractablesGroup = "interactables";

    private StandardMaterial3D _material = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 1.2f;
    public float InteractRadius => 0.3f;
    public bool IsAvailable => IsInstanceValid(this);
    public string Title => "Payphone - Supplier";
    public string Status => PhoneMenu.Status(DeliveryTarget());

    public override void _Ready()
    {
        AddToGroup(InteractablesGroup);
        _material = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.58f, 0.6f) };
        AddChild(new MeshInstance3D // pole
        {
            Mesh = new BoxMesh { Size = new Vector3(0.12f, 1.2f, 0.12f) },
            MaterialOverride = _material,
            Position = Vector3.Up * 0.6f,
        });
        AddChild(new MeshInstance3D // phone box
        {
            Mesh = new BoxMesh { Size = new Vector3(0.45f, 0.7f, 0.3f) },
            MaterialOverride = _material,
            Position = Vector3.Up * 1.5f,
        });
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.45f, 1.85f, 0.3f) }, Position = Vector3.Up * 0.925f });
        AddChild(new Label3D
        {
            Text = "PAYPHONE",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 28,
            OutlineSize = 8,
            PixelSize = 0.004f,
            Position = Vector3.Up * 2.2f,
        });
    }

    public Vector3 GetAccessPoint(Vector3 from)
    {
        var away = from - GlobalPosition;
        away.Y = 0f;
        var point = GlobalPosition + (away.LengthSquared() > 0.0001f ? away.Normalized() : Vector3.Forward) * 0.7f;
        point.Y = from.Y;
        return point;
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who) => PhoneMenu.GetOptions(DeliveryTarget(), who);

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }

    /// <summary>Which property gets the delivery. One property for now; a choice when you own several.</summary>
    private FacilityNode? DeliveryTarget() =>
        GetTree().GetFirstNodeInGroup(FacilityNode.Group) as FacilityNode;
}
