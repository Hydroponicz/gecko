using System;
using Godot;

namespace Gecko.World;

/// <summary>
/// Gray-box city for testing vehicles: roads, sidewalks and plain building blocks around the test lot.
/// Placeholder until the real 1990s New York-style map is designed. Everything is generated from the
/// lists below, so the layout is easy to change.
/// </summary>
public partial class CityBlockout : Node3D
{
    // Roads as (x0, z0, x1, z1) in world metres.
    private static readonly Rect2[] Roads =
    [
        Rect(21, -90, 29, 70),   // east avenue, runs past the warehouse loading bay
        Rect(-40, 22, 90, 30),   // south street
        Rect(-40, -90, -32, 70), // west avenue
        Rect(-40, -98, 90, -90), // north street
        Rect(29, -40, 90, -32),  // cross street east
    ];

    // Building blocks as (x0, z0, x1, z1, height).
    private static readonly (Rect2 Area, float Height)[] Buildings =
    [
        (Rect(33, -86, 52, -60), 18f), (Rect(54, -86, 86, -66), 26f), (Rect(54, -62, 86, -44), 12f),
        (Rect(33, -56, 52, -44), 9f),
        (Rect(33, -28, 50, -6), 14f), (Rect(52, -28, 86, -14), 22f), (Rect(52, -10, 86, 18), 10f),
        (Rect(33, -2, 48, 18), 16f),
        (Rect(-28, 34, 10, 60), 12f), (Rect(14, 34, 46, 66), 20f), (Rect(50, 34, 86, 60), 15f),
        (Rect(-86, -86, -44, -40), 24f), (Rect(-86, -34, -44, 18), 11f), (Rect(-86, 34, -44, 66), 17f),
        (Rect(-28, -86, 16, -50), 13f),
    ];

    private static Rect2 Rect(float x0, float z0, float x1, float z1) => new(x0, z0, x1 - x0, z1 - z0);

    public override void _Ready()
    {
        var asphalt = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.16f, 0.17f), Roughness = 0.9f };
        var stripe = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.75f, 0.2f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var sidewalk = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.54f, 0.5f) };

        foreach (var road in Roads)
        {
            // Sidewalk band around the road, then the road on top, then a centre line.
            AddFlat(road.Grow(2.5f), 0.01f, sidewalk);
            AddFlat(road, 0.02f, asphalt);
            bool northSouth = road.Size.Y > road.Size.X;
            var center = road.GetCenter();
            var line = northSouth
                ? new Rect2(center.X - 0.1f, road.Position.Y, 0.2f, road.Size.Y)
                : new Rect2(road.Position.X, center.Y - 0.1f, road.Size.X, 0.2f);
            AddFlat(line, 0.03f, stripe);
        }

        // Concrete apron from the east avenue to the warehouse loading bay.
        AddFlat(Rect(18, -18, 21, -12), 0.02f, sidewalk);

        var random = new Random(7);
        foreach (var (area, height) in Buildings)
        {
            var color = random.Next(3) switch
            {
                0 => new Color(0.5f, 0.3f, 0.24f),  // brick
                1 => new Color(0.45f, 0.43f, 0.4f), // concrete
                _ => new Color(0.38f, 0.33f, 0.3f), // brownstone
            };
            AddBuilding(area, height, new StandardMaterial3D { AlbedoColor = color });
        }
    }

    private void AddFlat(Rect2 area, float y, Material material)
    {
        var center = area.GetCenter();
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = area.Size },
            MaterialOverride = material,
            Position = new Vector3(center.X, y, center.Y),
        });
    }

    private void AddBuilding(Rect2 area, float height, Material material)
    {
        var center = area.GetCenter();
        var size = new Vector3(area.Size.X, height, area.Size.Y);
        var body = new StaticBody3D { Position = new Vector3(center.X, height / 2f, center.Y) };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }
}
