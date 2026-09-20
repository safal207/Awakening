using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

try
{
    Require((int)BuildingType.Lamp == 9,
        "existing BuildingType numeric values must remain stable");
    Require((int)BuildingType.Depot == 10,
        "Depot must be appended to the enum, not inserted into old values");
    Require(DepotDistrict.GateCount == 3, "depot must expose exactly three service gates");
    Require(DepotDistrict.TrackCount == 2, "depot must expose exactly two street track pairs");

    foreach (int seed in new[] { 1, 424242, 987654321 })
    {
        List<CityBlock> blocks = CityGenerator.Generate(seed);
        Require(blocks.Count == 441, "city block count must remain 21x21");

        int depotCount = 0;
        CityBlock depot = default;
        foreach (CityBlock block in blocks)
        {
            if (block.Type != BuildingType.Depot) continue;
            depotCount++;
            depot = block;
        }

        Require(depotCount == 1, $"seed {seed}: city must contain exactly one Depot block");
        Require(DepotDistrict.IsDepotBlock(depot),
            $"seed {seed}: depot must stay at deterministic center (0,0)");
        Require(depot.Height == 2,
            $"seed {seed}: depot must keep low two-storey industrial silhouette");

        var facade = new List<float>();
        var street = new List<float>();
        DepotDistrict.AppendFacade(facade, depot);
        DepotDistrict.AppendStreetProps(street, depot);

        Require(facade.Count > 9 * 20,
            $"seed {seed}: depot facade must emit non-trivial gate/roof geometry");
        Require(street.Count > 9 * 40,
            $"seed {seed}: depot street layer must emit rails/sleepers/gantry geometry");
        RequireFinite(facade, $"seed {seed}: depot facade");
        RequireFinite(street, $"seed {seed}: depot street props");
    }

    const int gameplaySeed = 424242;
    var progress = new HeroProgress();
    var city = new CityRenderer(gameplaySeed, progress);
    _ = new InteractionDetector(city);

    Require(city.NpcCount == 50, "depot visual identity must not change resident count");

    var stableIds = new HashSet<int>();
    for (int i = 0; i < city.Npcs.Count; i++)
    {
        int id = ResidentIdentity.GetPersistentId(city.Npcs[i]);
        Require(id == i, "persistent resident slot must remain deterministic");
        stableIds.Add(id);
    }
    Require(stableIds.Count == 50, "all 50 stable resident IDs must remain unique");

    // The depot replaces the generic parked car at the chapter curb, so the
    // track throat and existing First Memory interactions must remain reachable.
    foreach (Vector3 point in new[]
    {
        FirstMemorySpatial.SignalPosition,
        FirstMemoryFindings.All[0].Position,
        FirstMemoryFindings.All[1].Position,
        new Vector3(CityGenerator.BlockSize + 2f, 0f, 2.25f),
        new Vector3(CityGenerator.BlockSize + 2f, 0f, 5.30f),
    })
    {
        Require(city.IsPositionWalkable(point, 0.20f),
            $"chapter/depot point must remain walkable: {point}");
    }

    Console.WriteLine(
        "DEPOT_DISTRICT_SMOKE=PASS; depot=1; gates=3; tracks=2; residents=50; stable_ids=50");
}
catch (Exception e)
{
    Console.Error.WriteLine("DEPOT_DISTRICT_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}

static void RequireFinite(List<float> values, string label)
{
    foreach (float value in values)
        if (!float.IsFinite(value))
            throw new InvalidOperationException(label + " contains non-finite geometry");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
