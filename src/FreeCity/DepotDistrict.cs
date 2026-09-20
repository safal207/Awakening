using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Deterministic visual identity for the First Memory tram depot.
/// It adds no gameplay rewards or transport simulation; it only makes the
/// central chapter block legible as a depot through silhouette and street props.
/// </summary>
public static class DepotDistrict
{
    public const int GateCount = 3;
    public const int TrackCount = 2;

    public static bool IsDepotBlock(CityBlock block) =>
        block.X == 0 && block.Z == 0 && block.Type == BuildingType.Depot;

    public static CityBlock? FindDepot(IReadOnlyList<CityBlock> blocks)
    {
        if (blocks == null) return null;
        foreach (CityBlock block in blocks)
            if (IsDepotBlock(block)) return block;
        return null;
    }

    public static void AppendFacade(List<float> vertices, CityBlock block)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        if (!IsDepotBlock(block)) return;

        float x = block.X + 0.5f;
        float z = block.Z + 0.5f;
        float w = block.Width - 1f;
        float d = block.Depth - 1f;
        float h = block.Height * 2.5f;

        Vector3 gate = new(0.12f, 0.15f, 0.16f);
        Vector3 frame = new(0.48f, 0.50f, 0.46f);
        Vector3 warning = new(0.69f, 0.35f, 0.17f);
        Vector3 roof = new(0.30f, 0.34f, 0.34f);

        // Three tall service doors face the First Memory signal on the east side.
        float gateSpan = d / GateCount;
        for (int i = 0; i < GateCount; i++)
        {
            float centerZ = z + gateSpan * (i + 0.5f);
            float doorDepth = Math.Min(2.25f, gateSpan - 0.35f);
            float doorY = 0.18f;
            float doorH = Math.Min(3.05f, h - 0.55f);

            SceneGeometry.Box(
                vertices,
                new Vector3(x + w + 0.025f, doorY, centerZ - doorDepth * 0.5f),
                new Vector3(0.09f, doorH, doorDepth),
                gate);

            // Frame: two uprights + lintel.
            foreach (float side in new[] { -1f, 1f })
            {
                float sideZ = centerZ + side * doorDepth * 0.5f;
                SceneGeometry.Box(
                    vertices,
                    new Vector3(x + w + 0.075f, doorY, sideZ - 0.045f),
                    new Vector3(0.08f, doorH + 0.12f, 0.09f),
                    frame);
            }
            SceneGeometry.Box(
                vertices,
                new Vector3(x + w + 0.075f, doorY + doorH, centerZ - doorDepth * 0.5f),
                new Vector3(0.08f, 0.12f, doorDepth),
                frame);

            // Low warning stripe reads at distance without requiring world text.
            SceneGeometry.Box(
                vertices,
                new Vector3(x + w + 0.125f, doorY + 0.28f, centerZ - doorDepth * 0.42f),
                new Vector3(0.03f, 0.16f, doorDepth * 0.84f),
                warning);
        }

        // Long depot name-board silhouette and three roof monitors.
        SceneGeometry.Box(
            vertices,
            new Vector3(x + w + 0.08f, h - 0.78f, z + 0.95f),
            new Vector3(0.10f, 0.58f, d - 1.9f),
            warning * 0.82f);

        for (int i = 0; i < 3; i++)
        {
            float rz = z + 1.15f + i * 2.75f;
            SceneGeometry.Box(
                vertices,
                new Vector3(x + 1.15f, h + 0.04f, rz),
                new Vector3(w - 2.3f, 0.52f, 1.05f),
                roof);
        }
    }

    public static void AppendStreetProps(List<float> vertices, CityBlock block)
    {
        if (vertices == null) throw new ArgumentNullException(nameof(vertices));
        if (!IsDepotBlock(block)) return;

        Vector3 rail = new(0.42f, 0.44f, 0.42f);
        Vector3 sleeper = new(0.25f, 0.19f, 0.14f);
        Vector3 gantry = new(0.17f, 0.19f, 0.20f);

        float startX = block.X + block.Width - 0.4f;
        float endX = block.X + block.Width + CityGenerator.RoadWidth - 1.4f;
        float length = endX - startX;

        // Two visible track pairs leave the first two depot doors.
        for (int track = 0; track < TrackCount; track++)
        {
            float centerZ = block.Z + 2.25f + track * 3.05f;
            foreach (float side in new[] { -0.38f, 0.38f })
            {
                SceneGeometry.Box(
                    vertices,
                    new Vector3(startX, 0.135f, centerZ + side - 0.035f),
                    new Vector3(length, 0.055f, 0.07f),
                    rail);
            }

            for (float sx = startX; sx < endX; sx += 0.78f)
            {
                SceneGeometry.Box(
                    vertices,
                    new Vector3(sx, 0.105f, centerZ - 0.62f),
                    new Vector3(0.16f, 0.035f, 1.24f),
                    sleeper);
            }
        }

        // One overhead service gantry frames the track throat.
        float gx = block.X + block.Width + 7.2f;
        float leftZ = block.Z + 1.15f;
        float rightZ = block.Z + 7.15f;
        SceneGeometry.Cylinder(vertices, new(gx, 0.02f, leftZ), new(gx, 4.4f, leftZ), 0.06f, gantry, 8);
        SceneGeometry.Cylinder(vertices, new(gx, 0.02f, rightZ), new(gx, 4.4f, rightZ), 0.06f, gantry, 8);
        SceneGeometry.Cylinder(vertices, new(gx, 4.34f, leftZ), new(gx, 4.34f, rightZ), 0.055f, gantry, 8);

        // Hanging contact-line stubs make the structure read as tram infrastructure.
        foreach (float z in new[] { block.Z + 2.25f, block.Z + 5.30f })
        {
            SceneGeometry.Cylinder(vertices, new(gx, 4.34f, z), new(gx, 3.65f, z), 0.025f, gantry, 6);
        }
    }
}
