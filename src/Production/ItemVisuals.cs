using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>Placeholder meshes for items, built from <see cref="ItemDef"/> shape/size/color.</summary>
public static class ItemVisuals
{
    private static readonly Dictionary<ItemType, (Mesh Mesh, Material Material)> Cache = new();
    private static readonly Dictionary<ItemType, (Mesh Mesh, Material Material)> BandCache = new();

    public static MeshInstance3D Create(ItemType type)
    {
        var (mesh, material) = Get(type);
        var item = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };

        // Paper band around the middle (cash bundles and bricks).
        var def = Items.Get(type);
        if (def.BandColor is { } bandColor)
        {
            if (!BandCache.TryGetValue(type, out var band))
            {
                band = (new BoxMesh { Size = new Vector3(def.Size.X * 0.25f, def.Size.Y * 1.04f, def.Size.Z * 1.04f) },
                    new StandardMaterial3D { AlbedoColor = bandColor });
                BandCache[type] = band;
            }
            item.AddChild(new MeshInstance3D { Mesh = band.Mesh, MaterialOverride = band.Material });
        }
        return item;
    }

    /// <summary>Fills <paramref name="parent"/> with a vertical stack of items (capped to keep it readable).</summary>
    public static void BuildStack(Node3D parent, ItemType? type, int count, int maxShown = 10)
    {
        foreach (var child in parent.GetChildren())
            child.QueueFree();
        if (type is not { } itemType || count <= 0)
            return;

        var def = Items.Get(itemType);
        int shown = Mathf.Min(count, maxShown);
        for (int i = 0; i < shown; i++)
        {
            var item = Create(itemType);
            item.Position = new Vector3(0f, def.Size.Y * (i + 0.5f), 0f);
            parent.AddChild(item);
        }
    }

    private static (Mesh, Material) Get(ItemType type)
    {
        if (Cache.TryGetValue(type, out var cached))
            return cached;

        var def = Items.Get(type);
        Mesh mesh = def.Shape switch
        {
            ItemShape.Cylinder => new CylinderMesh
            {
                TopRadius = def.Size.X / 2f,
                BottomRadius = def.Size.X / 2f,
                Height = def.Size.Y,
            },
            _ => new BoxMesh { Size = def.Size },
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = def.Color,
            // Wet sheets look glossy.
            Roughness = type == ItemType.WetSheet ? 0.15f : 0.8f,
        };
        Cache[type] = (mesh, material);
        return (mesh, material);
    }
}
