using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

/// <summary>
/// Deterministic low-cost routing for the regular street grid of Mera.
/// It is deliberately not a general navmesh: direct conservative line-of-sight
/// stays direct; otherwise a point connects to a safe street intersection,
/// travels through connected street-center links, and returns to the target via
/// a conservative lane connector that avoids generic facade/car envelopes.
/// </summary>
public static class NpcStreetRouting
{
    private const float NpcRadius = 0.25f;
    private const float SidewalkRingLow = -2f;
    private const float SidewalkRingHigh = CityGenerator.BlockSize + 2f; // 12
    private const float StreetCenterOffset =
        CityGenerator.BlockSize + CityGenerator.SidewalkW + CityGenerator.CarriagewayWidth * 0.5f; // 19
    private const float DirectSampleStep = 0.5f;

    private readonly record struct Attachment(Vector3 Corner, Vector3 Intersection);

    public static IReadOnlyList<Vector3> Plan(Vector3 from, Vector3 destination)
    {
        var result = new List<Vector3>(6);
        BuildRoute(from, destination, result);
        return result;
    }

    internal static void BuildRoute(Vector3 from, Vector3 destination, List<Vector3> result)
    {
        result.Clear();
        if (!Finite(from) || !Finite(destination))
        {
            AddUnique(result, Finite(destination) ? Ground(destination) : Ground(from));
            return;
        }

        destination = Ground(destination);
        if (SegmentGenericallyClear(from, destination))
        {
            AddUnique(result, destination);
            return;
        }

        Attachment start = Attach(from);
        Attachment end = Attach(destination);

        AppendStartApproach(from, start, result);

        if (HorizontalDistanceSquared(start.Intersection, end.Intersection) > 0.01f)
        {
            Vector3 bendXThenZ = Ground(new Vector3(end.Intersection.X, 0f, start.Intersection.Z));
            Vector3 bendZThenX = Ground(new Vector3(start.Intersection.X, 0f, end.Intersection.Z));

            float xFirst = HorizontalDistance(start.Intersection, bendXThenZ) +
                           HorizontalDistance(bendXThenZ, end.Intersection);
            float zFirst = HorizontalDistance(start.Intersection, bendZThenX) +
                           HorizontalDistance(bendZThenX, end.Intersection);

            // Both bends lie on connected street-center lines. Stable tie-break
            // avoids route jitter between equivalent Manhattan paths.
            AddUnique(result, xFirst <= zFirst ? bendXThenZ : bendZThenX);
            AddUnique(result, end.Intersection);
        }

        AppendEndApproach(end, destination, result);

        if (result.Count == 0)
            result.Add(destination);
    }

    /// <summary>
    /// Wander targets are arbitrary random points and can land inside a building
    /// or parked-car envelope. Home/work positions are already city-clamped, but
    /// wander targets need a deterministic safe normalization before routing.
    /// </summary>
    internal static Vector3 NormalizeWanderDestination(Vector3 destination)
    {
        destination = Ground(destination);
        if (GenericallyWalkable(destination)) return destination;
        return Attach(destination).Corner;
    }

    public static bool RouteUsesOnlyGenericSafeWaypoints(IReadOnlyList<Vector3> route)
    {
        for (int i = 0; i < route.Count; i++)
        {
            if (!Finite(route[i])) return false;
            // The final destination can intentionally be inside a generic block
            // when the real seeded cell is an open Tree/Lamp cell. Intermediate
            // routing nodes must be universally safe.
            if (i < route.Count - 1 && !GenericallyWalkable(route[i]))
                return false;
        }
        return true;
    }

    private static void AppendStartApproach(Vector3 from, Attachment attachment, List<Vector3> route)
    {
        Vector3 intersection = attachment.Intersection;
        if (SegmentGenericallyClear(from, intersection))
        {
            AddUnique(route, intersection);
            return;
        }

        Vector3 laneX = Ground(new Vector3(intersection.X, 0f, from.Z));
        Vector3 laneZ = Ground(new Vector3(from.X, 0f, intersection.Z));
        bool xClear = SegmentGenericallyClear(from, laneX) && SegmentGenericallyClear(laneX, intersection);
        bool zClear = SegmentGenericallyClear(from, laneZ) && SegmentGenericallyClear(laneZ, intersection);

        if (xClear || zClear)
        {
            Vector3 lane = ChooseShorterLane(from, intersection, laneX, xClear, laneZ, zClear);
            AddUnique(route, lane);
            AddUnique(route, intersection);
            return;
        }

        // Fallback for a point that is generically blocked only because its real
        // seeded cell is an open Tree/Lamp cell. The nearest sidewalk corner is
        // deterministic and the real city collision layer remains the final guard.
        AddUnique(route, attachment.Corner);
        AddUnique(route, intersection);
    }

    private static void AppendEndApproach(Attachment attachment, Vector3 destination, List<Vector3> route)
    {
        Vector3 intersection = attachment.Intersection;
        if (SegmentGenericallyClear(intersection, destination))
        {
            AddUnique(route, destination);
            return;
        }

        Vector3 laneX = Ground(new Vector3(intersection.X, 0f, destination.Z));
        Vector3 laneZ = Ground(new Vector3(destination.X, 0f, intersection.Z));
        bool xClear = SegmentGenericallyClear(intersection, laneX) && SegmentGenericallyClear(laneX, destination);
        bool zClear = SegmentGenericallyClear(intersection, laneZ) && SegmentGenericallyClear(laneZ, destination);

        if (xClear || zClear)
        {
            Vector3 lane = ChooseShorterLane(intersection, destination, laneX, xClear, laneZ, zClear);
            AddUnique(route, lane);
            AddUnique(route, destination);
            return;
        }

        AddUnique(route, attachment.Corner);
        AddUnique(route, destination);
    }

    private static Vector3 ChooseShorterLane(
        Vector3 from,
        Vector3 to,
        Vector3 laneA,
        bool aClear,
        Vector3 laneB,
        bool bClear)
    {
        if (!aClear) return laneB;
        if (!bClear) return laneA;

        float aLength = HorizontalDistance(from, laneA) + HorizontalDistance(laneA, to);
        float bLength = HorizontalDistance(from, laneB) + HorizontalDistance(laneB, to);
        return aLength <= bLength ? laneA : laneB;
    }

    private static Attachment Attach(Vector3 point)
    {
        point = Ground(point);
        float ox = CellOrigin(point.X);
        float oz = CellOrigin(point.Z);

        Vector3[] corners =
        {
            Ground(new Vector3(ox + SidewalkRingLow, 0f, oz + SidewalkRingLow)),
            Ground(new Vector3(ox + SidewalkRingLow, 0f, oz + SidewalkRingHigh)),
            Ground(new Vector3(ox + SidewalkRingHigh, 0f, oz + SidewalkRingLow)),
            Ground(new Vector3(ox + SidewalkRingHigh, 0f, oz + SidewalkRingHigh)),
        };

        int best = 0;
        float bestDistance = HorizontalDistanceSquared(point, corners[0]);
        for (int i = 1; i < corners.Length; i++)
        {
            float distance = HorizontalDistanceSquared(point, corners[i]);
            if (distance < bestDistance - 0.0001f)
            {
                best = i;
                bestDistance = distance;
            }
        }

        Vector3 corner = corners[best];
        bool west = best is 0 or 1;
        bool north = best is 0 or 2;
        float intersectionX = west
            ? ox + StreetCenterOffset - CityGenerator.CellSize
            : ox + StreetCenterOffset;
        float intersectionZ = north
            ? oz + StreetCenterOffset - CityGenerator.CellSize
            : oz + StreetCenterOffset;

        return new Attachment(
            corner,
            Ground(new Vector3(intersectionX, 0f, intersectionZ)));
    }

    private static bool SegmentGenericallyClear(Vector3 from, Vector3 to)
    {
        float distance = HorizontalDistance(from, to);
        int samples = Math.Max(1, (int)MathF.Ceiling(distance / DirectSampleStep));
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (!GenericallyWalkable(p)) return false;
        }
        return true;
    }

    private static bool GenericallyWalkable(Vector3 p)
    {
        if (!Finite(p)) return false;
        float lx = Local(p.X);
        float lz = Local(p.Z);

        // Treat every block as if it contained a full building. This is
        // conservative for Tree/Lamp cells but guarantees a planned intermediate
        // route never depends on a particular visual building type.
        float minBuilding = 0.5f - NpcRadius;
        float maxBuilding = CityGenerator.BlockSize - 0.5f + NpcRadius;
        if (lx >= minBuilding && lx <= maxBuilding &&
            lz >= minBuilding && lz <= maxBuilding)
            return false;

        // Cars use a deterministic location for ordinary blocks. Conservatively
        // reserve that envelope in every cell so route nodes never rely on the
        // absence of a particular car.
        const float carCenterX = CityGenerator.BlockSize + CityGenerator.SidewalkW + 1.2f;
        const float carCenterZ = 5f;
        float carMinX = carCenterX - 0.92f - NpcRadius;
        float carMaxX = carCenterX + 0.92f + NpcRadius;
        float carMinZ = carCenterZ - 2.2f - NpcRadius;
        float carMaxZ = carCenterZ + 2.2f + NpcRadius;
        if (lx >= carMinX && lx <= carMaxX && lz >= carMinZ && lz <= carMaxZ)
            return false;

        return true;
    }

    private static float CellOrigin(float coordinate) =>
        MathF.Floor(coordinate / CityGenerator.CellSize) * CityGenerator.CellSize;

    private static float Local(float coordinate)
    {
        float local = coordinate - CellOrigin(coordinate);
        return local < 0f ? local + CityGenerator.CellSize : local;
    }

    private static Vector3 Ground(Vector3 p)
    {
        if (!Finite(p)) return p;
        p.Y = CityGenerator.GroundHeight(p.X, p.Z);
        return p;
    }

    private static void AddUnique(List<Vector3> route, Vector3 point)
    {
        if (!Finite(point)) return;
        if (route.Count > 0 && HorizontalDistanceSquared(route[^1], point) < 0.01f)
            return;
        route.Add(point);
    }

    private static bool Finite(Vector3 p) =>
        float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    private static float HorizontalDistance(Vector3 a, Vector3 b) =>
        MathF.Sqrt(HorizontalDistanceSquared(a, b));

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }
}
