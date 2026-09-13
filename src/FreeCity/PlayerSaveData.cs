using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public sealed class PlayerSaveData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public int? InteriorCellX { get; set; }
    public int? InteriorCellZ { get; set; }

    internal Vector3 Position => new(X,Y,Z);

    internal static PlayerSaveData Capture(NpcCharacter player, CityBlock? interior) => new()
    {
        X = player.Position.X, Y = player.Position.Y, Z = player.Position.Z,
        Yaw = MathF.Atan2(MathF.Sin(player.Rotation),MathF.Cos(player.Rotation)),
        InteriorCellX = interior?.X / CityGenerator.CellSize,
        InteriorCellZ = interior?.Z / CityGenerator.CellSize,
    };

    internal CityBlock? FindInterior(IReadOnlyList<CityBlock> blocks)
    {
        if (!InteriorCellX.HasValue) return null;
        foreach (var block in blocks)
            if (block.X == InteriorCellX * CityGenerator.CellSize && block.Z == InteriorCellZ * CityGenerator.CellSize &&
                block.Type is not (BuildingType.Tree or BuildingType.Lamp)) return block;
        throw new InvalidDataException("Saved interior is unavailable.");
    }

    internal void Validate(int seed)
    {
        if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Z) || !float.IsFinite(Yaw) ||
            Math.Abs(X) > 10000 || Math.Abs(Z) > 10000 || Math.Abs(Y) > 1000 || Math.Abs(Yaw) > MathHelper.TwoPi ||
            InteriorCellX.HasValue != InteriorCellZ.HasValue)
            throw new InvalidDataException("Invalid player location.");
        if (InteriorCellX.HasValue)
        {
            if (Math.Abs((long)InteriorCellX.Value) > CityGenerator.CityRadius || Math.Abs((long)InteriorCellZ!.Value) > CityGenerator.CityRadius)
                throw new InvalidDataException("Invalid interior id.");
            var block = FindInterior(CityGenerator.Generate(seed))!.Value;
            if (X < block.X || X > block.X+block.Width || Z < block.Z || Z > block.Z+block.Depth)
                throw new InvalidDataException("Player is outside saved interior.");
        }
    }
}
