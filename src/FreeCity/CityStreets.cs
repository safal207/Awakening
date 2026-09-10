using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class CityStreets
{
    internal static List<float> BuildRoads()
    {
        var v = new List<float>();
        int cell = CityGenerator.BlockSize + CityGenerator.RoadWidth;
        int low = -CityGenerator.CityRadius * cell - CityGenerator.RoadWidth;
        int high = CityGenerator.CityRadius * cell + cell;
        SceneGeometry.Ground(v, low, low, high-low, high-low, -0.04f, new(0.25f,0.34f,0.28f));
        Vector3 asphalt = new(0.19f,0.22f,0.24f), paint = new(0.83f,0.83f,0.71f);
        for (int i = -CityGenerator.CityRadius-1; i <= CityGenerator.CityRadius; i++)
        {
            float road = i * cell + CityGenerator.BlockSize;
            SceneGeometry.Ground(v, road, low, CityGenerator.RoadWidth, high-low, 0, asphalt);
            SceneGeometry.Ground(v, low, road, high-low, CityGenerator.RoadWidth, 0, asphalt);
            for (int j = -CityGenerator.CityRadius; j <= CityGenerator.CityRadius; j++)
            {
                float block = j * cell;
                for (int dash = 0; dash < 2; dash++)
                {
                    float at = block + 1 + dash * 5;
                    SceneGeometry.Ground(v, road+1.94f, at, 0.12f, 2.4f, 0.006f, paint);
                    SceneGeometry.Ground(v, at, road+1.94f, 2.4f, 0.12f, 0.006f, paint);
                }
                // Crossings are outside building footprints and clear of the junction center.
                for (int stripe = 0; stripe < 5; stripe++)
                {
                    float across = road + 0.30f + stripe * 0.70f;
                    SceneGeometry.Ground(v, across, block+8.2f, 0.42f, 1.3f, 0.008f, paint);
                    SceneGeometry.Ground(v, block+8.2f, across, 1.3f, 0.42f, 0.008f, paint);
                }
            }
        }
        return v;
    }

    internal static List<float> BuildPaving(IReadOnlyList<CityBlock> blocks)
    {
        var v = new List<float>();
        foreach (var b in blocks)
        {
            Vector3 paving = new(0.56f,0.60f,0.58f);
            SceneGeometry.Ground(v, b.X, b.Z, b.Width, b.Depth, 0.018f, paving);
            Vector3 curb = new(0.70f,0.72f,0.69f);
            SceneGeometry.Ground(v, b.X, b.Z, b.Width, 0.12f, 0.022f, curb);
            SceneGeometry.Ground(v, b.X, b.Z+b.Depth-0.12f, b.Width, 0.12f, 0.022f, curb);
            SceneGeometry.Ground(v, b.X, b.Z, 0.12f, b.Depth, 0.022f, curb);
            SceneGeometry.Ground(v, b.X+b.Width-0.12f, b.Z, 0.12f, b.Depth, 0.022f, curb);
            if (b.Type is BuildingType.Tree or BuildingType.Lamp)
            {
                SceneGeometry.Ground(v, b.X+1.2f, b.Z+1.2f, b.Width-2.4f, b.Depth-2.4f, 0.024f, new(0.27f,0.44f,0.31f));
                SceneGeometry.Ground(v, b.X+4.3f, b.Z, 1.4f, b.Depth, 0.026f, paving);
            }
        }
        return v;
    }
}
