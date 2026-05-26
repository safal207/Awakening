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
    public const int RoadWidth = 4;      // ширина дороги
    public const int SidewalkW = 1;      // ширина тротуара
    public const int CityRadius = 10;    // кварталов в стороны от центра

    private static readonly Vector3[] AccentColors =
    {
        new(0.9f, 0.3f, 0.3f),
        new(0.3f, 0.6f, 0.9f),
        new(0.9f, 0.8f, 0.2f),
        new(0.2f, 0.8f, 0.3f),
        new(1f, 0.5f, 0f),
        new(0.7f, 0.3f, 0.8f),
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
        return new Vector3(
            0.3f + (float)rng.NextDouble() * 0.5f,
            0.3f + (float)rng.NextDouble() * 0.5f,
            0.3f + (float)rng.NextDouble() * 0.5f
        );
    }

    private static Vector3 PickAccent(Random rng)
    {
        return AccentColors[rng.Next(AccentColors.Length)];
    }

    public static int WorldToBlock(float w) => (int)MathF.Floor(w / (BlockSize + RoadWidth));
}
