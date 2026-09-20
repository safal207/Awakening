using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public enum BuildingType
{
    Office,      // серый стеклянный офис
    Bank,        // банк с колоннами
    Cafe,        // кафе с красной крышей
    Apartment,   // жилой дом
    Store,       // магазин
    Police,      // полицейский участок
    GasStation,  // заправка
    House,       // маленький домик
    Tree,        // дерево
    Lamp,        // фонарь
    Depot,       // центральное трамвайное депо First Memory
}

public struct CityBlock
{
    public int X, Z;           // координаты квартала (в блоках)
    public int Width, Depth;   // размер квартала
    public BuildingType Type;
    public int Height;         // этажность
    public Vector3 Color;      // основной цвет
    public Vector3 Accent;     // акцентный цвет
}

public static class CityGenerator
{
    public const int BlockSize = 10;     // размер квартала
    public const int RoadWidth = 18;     // Street corridor, including both sidewalks.
    public const int SidewalkW = 3;
    public const int CarriagewayWidth = RoadWidth - SidewalkW * 2;
    public const int CellSize = BlockSize + RoadWidth;
    public const int CityRadius = 10;    // кварталов в стороны от центра

    private static readonly Vector3[] AccentColors =
    {
        new(0.68f, 0.28f, 0.28f),
        new(0.28f, 0.45f, 0.57f),
        new(0.78f, 0.62f, 0.31f),
        new(0.29f, 0.48f, 0.38f),
        new(0.69f, 0.43f, 0.30f),
        new(0.50f, 0.39f, 0.56f),
    };

    private static readonly Vector3[] FacadeColors =
    {
        new(0.50f,0.61f,0.66f), new(0.76f,0.77f,0.70f),
        new(0.60f,0.39f,0.36f), new(0.46f,0.60f,0.51f),
        new(0.56f,0.53f,0.61f), new(0.45f,0.49f,0.51f),
    };

    public static List<CityBlock> Generate(int seed)
    {
        var blocks = new List<CityBlock>((CityRadius * 2 + 1) * (CityRadius * 2 + 1));

        for (int dx = -CityRadius; dx <= CityRadius; dx++)
            for (int dz = -CityRadius; dz <= CityRadius; dz++)
            {
                var rng = new Random(MixSeed(seed, dx, dz));
                var block = new CityBlock
                {
                    X = dx * (BlockSize + RoadWidth),
                    Z = dz * (BlockSize + RoadWidth),
                    Width = BlockSize,
                    Depth = BlockSize,
                    Type = PickType(rng, dx, dz),
                    Height = PickHeight(rng),
                    Color = PickColor(rng),
                    Accent = PickAccent(rng),
                };

                // Override only after every RNG draw above has happened, so adding
                // the narrative depot does not reshuffle any seeded city state.
                if (dx == 0 && dz == 0)
                {
                    block.Type = BuildingType.Depot;
                    block.Height = 2;
                    block.Color = new Vector3(0.43f, 0.47f, 0.46f);
                    block.Accent = new Vector3(0.68f, 0.33f, 0.17f);
                }

                blocks.Add(block);
            }

        return blocks;
    }

    private static int MixSeed(int seed, int dx, int dz)
    {
        unchecked
        {
            int hash = seed;
            hash = (hash * 397) ^ dx;
            hash = (hash * 397) ^ dz;
            return hash;
        }
    }

    private static BuildingType PickType(Random rng, int dx, int dz)
    {
        double r = rng.NextDouble();
        int dist = Math.Abs(dx) + Math.Abs(dz);

        if (dist <= 2)
        {
            if (r < 0.20) return BuildingType.Bank;
            if (r < 0.50) return BuildingType.Office;
            if (r < 0.65) return BuildingType.Store;
            if (r < 0.80) return BuildingType.Cafe;
            if (r < 0.90) return BuildingType.Police;
            return BuildingType.GasStation;
        }
        else if (dist <= 6)
        {
            if (r < 0.35) return BuildingType.Office;
            if (r < 0.60) return BuildingType.Apartment;
            if (r < 0.75) return BuildingType.Store;
            if (r < 0.85) return BuildingType.Cafe;
            if (r < 0.95) return BuildingType.Bank;
            return BuildingType.Police;
        }
        else
        {
            if (r < 0.50) return BuildingType.House;
            if (r < 0.70) return BuildingType.Apartment;
            if (r < 0.85) return BuildingType.Tree;
            return BuildingType.Lamp;
        }
    }

    private static int PickHeight(Random rng)
    {
        double r = rng.NextDouble();
        if (r < 0.10) return 1;  // одноэтажное
        if (r < 0.30) return 2;  // двухэтажное
        if (r < 0.55) return 3;  // трёхэтажное
        if (r < 0.75) return 5;  // 5 этажей
        if (r < 0.90) return 8;  // 8 этажей
        return 12;               // небоскрёб
    }

    private static Vector3 PickColor(Random rng)
    {
        // Preserve the RNG draw count so a visual change does not reshuffle saved cities.
        int index = (int)(rng.NextDouble() * FacadeColors.Length);
        float brightness = 0.93f + (float)rng.NextDouble() * 0.12f;
        float warmth = ((float)rng.NextDouble() - 0.5f) * 0.025f;
        return FacadeColors[index] * brightness + new Vector3(warmth,0,-warmth);
    }

    private static Vector3 PickAccent(Random rng)
    {
        return AccentColors[rng.Next(AccentColors.Length)];
    }

    public static int WorldToBlock(float w) => (int)MathF.Floor(w / (BlockSize + RoadWidth));

    public static float GroundHeight(float x, float z)
    {
        float Local(float p) => p - MathF.Floor((p + SidewalkW) / CellSize) * CellSize;
        float lx = Local(x), lz = Local(z);
        return lx <= BlockSize + SidewalkW && lz <= BlockSize + SidewalkW ? 0.12f : 0f;
    }
}
