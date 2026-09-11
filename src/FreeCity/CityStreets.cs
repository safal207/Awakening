using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class CityStreets
{
    internal static List<float> BuildRoads()
    {
        var v = new List<float>();
        int cell = CityGenerator.CellSize, side = CityGenerator.SidewalkW;
        int low = -CityGenerator.CityRadius * cell - CityGenerator.RoadWidth;
        int high = CityGenerator.CityRadius * cell + cell;
        SceneGeometry.Ground(v, low, low, high-low, high-low, 0, new(0.22f,0.24f,0.25f));
        Vector3 white = new(0.82f,0.82f,0.76f), yellow = new(0.87f,0.66f,0.22f);
        for (int i = -CityGenerator.CityRadius-1; i <= CityGenerator.CityRadius; i++)
        {
            float edge = i * cell + CityGenerator.BlockSize + side;
            float mid = edge + CityGenerator.CarriagewayWidth * 0.5f;
            for (int j = -CityGenerator.CityRadius; j <= CityGenerator.CityRadius; j++)
            {
                float start = j * cell - side, end = j * cell + CityGenerator.BlockSize + side;
                foreach (float offset in new[] { -0.15f, 0.15f })
                {
                    SceneGeometry.Ground(v,mid+offset,start+2.6f,0.09f,end-start-5.2f,0.008f,yellow);
                    SceneGeometry.Ground(v,start+2.6f,mid+offset,end-start-5.2f,0.09f,0.008f,yellow);
                }
                for (int stripe = 0; stripe < 10; stripe++)
                {
                    float across = edge + 0.5f + stripe * 1.1f;
                    foreach (float crossing in new[] { start+0.3f, end-2.1f })
                    {
                        SceneGeometry.Ground(v,across,crossing,0.62f,1.8f,0.01f,white);
                        SceneGeometry.Ground(v,crossing,across,1.8f,0.62f,0.01f,white);
                    }
                }
                foreach (float offset in new[] { 2.4f, CityGenerator.CarriagewayWidth-2.4f })
                {
                    SceneGeometry.Ground(v,edge+offset,start+3.2f,0.06f,9.6f,0.007f,white*0.85f);
                    SceneGeometry.Ground(v,start+3.2f,edge+offset,9.6f,0.06f,0.007f,white*0.85f);
                }
            }
        }
        return v;
    }

    internal static List<float> BuildPaving(IReadOnlyList<CityBlock> blocks)
    {
        var v = new List<float>();
        float s = CityGenerator.SidewalkW;
        foreach (var b in blocks)
        {
            Vector3 paving = new(0.61f,0.63f,0.61f);
            SceneGeometry.Box(v,new(b.X-s,0,b.Z-s),new(b.Width+s*2,0.12f,b.Depth+s*2),paving);
            // Level apron across each crossing leaves a clear pedestrian route.
            SceneGeometry.Ground(v,b.X+b.Width+s-1.8f,b.Z+s,1.6f,2.2f,0.125f,new(0.74f,0.66f,0.42f));
        }
        return v;
    }
}
