using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;

const int Seed = 424242;
const float Radius = 0.20f;
const float CollisionRadius = 0.25f;
const float Dt = 0.10f;
const float MaxWalkingStallSeconds = 8f;
const int Steps = 2600; // 26 in-game hours at the current 0.1 h/s world clock.

try
{
    var city = new CityRenderer(Seed, new HeroProgress());
    Require(city.Npcs.Count == 50, "route smoke expects exactly 50 city residents including the player");

    int routesChecked = 0;
    int maxWaypoints = 0;
    for (int i = 0; i < city.Npcs.Count; i++)
    {
        NpcCharacter npc = city.Npcs[i];
        CheckRoute(city, npc.HomePos, npc.WorkPos, $"resident {i} home->work", ref routesChecked, ref maxWaypoints);
        CheckRoute(city, npc.WorkPos, npc.HomePos, $"resident {i} work->home", ref routesChecked, ref maxWaypoints);
    }

    float timeOfDay = 8f;
    int resets = 0;
    int count = city.Npcs.Count;
    var previous = new Vector3[count];
    var stallSeconds = new float[count];
    var maxStallSeconds = new float[count];
    var travelled = new float[count];
    for (int i = 0; i < count; i++)
        previous[i] = city.Npcs[i].Position;

    for (int step = 0; step < Steps; step++)
    {
        // Headless equivalent of the non-rendering portion of CityRenderer.UpdateNpcs:
        // advance world time, apply Sverka/reset at midnight, update each NPC and
        // retain the existing collision clamp as the final numerical safety net.
        timeOfDay += Dt * 0.1f;
        if (timeOfDay >= 24f)
        {
            timeOfDay -= 24f;
            city.Progress.NewDay();
            foreach (NpcCharacter npc in city.Npcs)
                npc.Reset();
            resets++;

            // A reset is an intentional teleport home, not routed travel or a stall.
            for (int i = 0; i < count; i++)
            {
                previous[i] = city.Npcs[i].Position;
                stallSeconds[i] = 0f;
            }
        }

        for (int i = 1; i < count; i++) // player is controlled elsewhere, 49 residents are AI-routed
        {
            NpcCharacter npc = city.Npcs[i];
            npc.Update(timeOfDay, Dt);
            if (npc.State != NpcState.Sleeping)
                npc.Position = city.ClampToWalkable(npc.Position, CollisionRadius);

            Require(Finite(npc.Position), $"resident {i} produced non-finite position at step {step}");
            Require(city.IsPositionWalkable(npc.Position, Radius),
                $"resident {i} ended inside static obstacle at step {step}: {npc.Position}");

            float moved = HorizontalDistance(previous[i], npc.Position);
            travelled[i] += moved;
            if (npc.State == NpcState.Walking && moved < 0.003f)
                stallSeconds[i] += Dt;
            else
                stallSeconds[i] = 0f;

            maxStallSeconds[i] = Math.Max(maxStallSeconds[i], stallSeconds[i]);
            Require(stallSeconds[i] <= MaxWalkingStallSeconds,
                $"resident {i} remained Walking without movement for {stallSeconds[i]:F1}s");
            previous[i] = npc.Position;
        }
    }

    Require(resets >= 1, "route soak must cross at least one midnight reset");

    float worstStall = 0f;
    float minTravelled = float.MaxValue;
    for (int i = 1; i < count; i++)
    {
        worstStall = Math.Max(worstStall, maxStallSeconds[i]);
        minTravelled = Math.Min(minTravelled, travelled[i]);
        Require(travelled[i] > 5f,
            $"resident {i} did not demonstrate meaningful movement over the day/night simulation");
    }

    Console.WriteLine(
        $"NPC_ROUTE_SMOKE=PASS; residents=50; routed_npcs=49; routes={routesChecked}; resets={resets}; " +
        $"max_waypoints={maxWaypoints}; worst_walking_stall={worstStall:F1}s; min_travel={minTravelled:F1}m");
}
catch (Exception e)
{
    Console.Error.WriteLine("NPC_ROUTE_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}

static void CheckRoute(
    CityRenderer city,
    Vector3 start,
    Vector3 destination,
    string label,
    ref int routesChecked,
    ref int maxWaypoints)
{
    IReadOnlyList<Vector3> route = NpcStreetRouting.Plan(start, destination);
    Require(route.Count > 0 && route.Count <= 6,
        $"{label}: expected 1..6 deterministic waypoints, got {route.Count}");
    Require(NpcStreetRouting.RouteUsesOnlyGenericSafeWaypoints(route),
        $"{label}: route contains non-finite or generically blocked intermediate waypoint");

    Vector3 segmentStart = start;
    for (int i = 0; i < route.Count; i++)
    {
        Vector3 segmentEnd = route[i];
        Require(SegmentWalkable(city, segmentStart, segmentEnd),
            $"{label}: segment {i} intersects actual static obstacle: {segmentStart} -> {segmentEnd}");
        segmentStart = segmentEnd;
    }

    Require(HorizontalDistance(segmentStart, destination) < 0.05f,
        $"{label}: final route point does not match destination");
    routesChecked++;
    maxWaypoints = Math.Max(maxWaypoints, route.Count);
}

static bool SegmentWalkable(CityRenderer city, Vector3 from, Vector3 to)
{
    float distance = HorizontalDistance(from, to);
    int samples = Math.Max(1, (int)MathF.Ceiling(distance / 0.25f));
    for (int i = 0; i <= samples; i++)
    {
        Vector3 p = Vector3.Lerp(from, to, i / (float)samples);
        p.Y = CityGenerator.GroundHeight(p.X, p.Z);
        if (!city.IsPositionWalkable(p, Radius))
            return false;
    }
    return true;
}

static bool Finite(Vector3 p) =>
    float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

static float HorizontalDistance(Vector3 a, Vector3 b)
{
    float dx = a.X - b.X;
    float dz = a.Z - b.Z;
    return MathF.Sqrt(dx * dx + dz * dz);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
