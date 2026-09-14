using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    public sealed class PlayerSpatialSaveData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Rotation { get; set; }
        public bool IsInside { get; set; }
        public int? InteriorBlockX { get; set; }
        public int? InteriorBlockZ { get; set; }
    }
}

public readonly record struct ResolvedPlayerSpatial(
    Vector3 Position,
    float Rotation,
    CityBlock? InteriorBlock);

/// <summary>
/// Bridges save/load data to a live CityRenderer without putting OpenGL-dependent
/// interior construction into SaveSystem. Validation and resolution stay pure;
/// application happens only after the game window and city are initialized.
/// </summary>
public static class PlayerSpatialPersistence
{
    private sealed class CityBox
    {
        public required CityRenderer City;
    }

    private sealed class PendingBox
    {
        public required SaveSystem.PlayerSpatialSaveData Data;
    }

    private static readonly ConditionalWeakTable<NpcCharacter, CityBox> CitiesByPlayer = new();
    private static readonly ConditionalWeakTable<HeroProgress, PendingBox> PendingByProgress = new();

    public static void RegisterCity(CityRenderer city)
    {
        if (city.Player == null) return;

        CitiesByPlayer.Remove(city.Player);
        CitiesByPlayer.Add(city.Player, new CityBox { City = city });

        if (!PendingByProgress.TryGetValue(city.Progress, out PendingBox? pending)) return;
        PendingByProgress.Remove(city.Progress);

        if (!TryApply(city, pending.Data))
        {
            city.Progress.SaveWritesBlocked = true;
            Console.WriteLine("Player spatial state was invalid; safe spawn kept and automatic writes are blocked.");
        }
    }

    public static void SetPending(HeroProgress progress, SaveSystem.PlayerSpatialSaveData? data)
    {
        PendingByProgress.Remove(progress);
        if (data != null)
            PendingByProgress.Add(progress, new PendingBox { Data = data });
    }

    public static SaveSystem.PlayerSpatialSaveData? Capture(IReadOnlyList<NpcCharacter>? npcs)
    {
        if (npcs == null || npcs.Count == 0) return null;
        NpcCharacter player = npcs[0];
        if (!CitiesByPlayer.TryGetValue(player, out CityBox? box)) return null;

        CityRenderer city = box.City;
        CityBlock? interior = city.InsideBlock;
        return new SaveSystem.PlayerSpatialSaveData
        {
            X = player.Position.X,
            Y = player.Position.Y,
            Z = player.Position.Z,
            Rotation = player.Rotation,
            IsInside = city.IsInside,
            InteriorBlockX = interior?.X,
            InteriorBlockZ = interior?.Z,
        };
    }

    public static bool TryResolve(
        CityRenderer city,
        SaveSystem.PlayerSpatialSaveData? data,
        out ResolvedPlayerSpatial resolved)
    {
        resolved = default;
        if (city.Player == null || data == null) return false;
        if (!float.IsFinite(data.X) || !float.IsFinite(data.Y) ||
            !float.IsFinite(data.Z) || !float.IsFinite(data.Rotation)) return false;

        if (!WithinWorldEnvelope(city, data.X, data.Z)) return false;
        float rotation = NormalizeRotation(data.Rotation);

        if (!data.IsInside)
        {
            Vector3 exterior = city.ClampToWalkable(new Vector3(data.X, data.Y, data.Z), 0.3f);
            resolved = new ResolvedPlayerSpatial(exterior, rotation, null);
            return true;
        }

        if (!data.InteriorBlockX.HasValue || !data.InteriorBlockZ.HasValue) return false;

        CityBlock? found = null;
        foreach (CityBlock block in city.Blocks)
        {
            if (block.X != data.InteriorBlockX.Value || block.Z != data.InteriorBlockZ.Value) continue;
            if (block.Type is BuildingType.Tree or BuildingType.Lamp) return false;
            found = block;
            break;
        }
        if (found is not CityBlock interior) return false;

        const float safeInset = 0.6f; // wall inset 0.3 + player radius 0.3
        Vector3 inside = new(
            Math.Clamp(data.X, interior.X + safeInset, interior.X + interior.Width - safeInset),
            0f,
            Math.Clamp(data.Z, interior.Z + safeInset, interior.Z + interior.Depth - safeInset));

        resolved = new ResolvedPlayerSpatial(inside, rotation, interior);
        return true;
    }

    private static bool TryApply(CityRenderer city, SaveSystem.PlayerSpatialSaveData data)
    {
        if (!TryResolve(city, data, out ResolvedPlayerSpatial resolved) || city.Player == null)
            return false;

        if (resolved.InteriorBlock is CityBlock block)
        {
            float doorX = block.X + block.Width * 0.5f;
            float doorZ = block.Z + block.Depth;
            if (!city.TryEnterInterior(new Vector3(doorX, 0f, doorZ)).HasValue)
                return false;
            city.Player.Position = city.ClampPlayerToWalkable(resolved.Position, 0.3f);
        }
        else
        {
            city.Player.Position = resolved.Position;
        }

        city.Player.Rotation = resolved.Rotation;
        city.Player.TargetRotation = resolved.Rotation;
        city.Player.Velocity = Vector3.Zero;
        return true;
    }

    private static bool WithinWorldEnvelope(CityRenderer city, float x, float z)
    {
        if (city.Blocks.Count == 0) return false;

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;
        foreach (CityBlock block in city.Blocks)
        {
            minX = Math.Min(minX, block.X);
            minZ = Math.Min(minZ, block.Z);
            maxX = Math.Max(maxX, block.X + block.Width);
            maxZ = Math.Max(maxZ, block.Z + block.Depth);
        }

        float margin = CityGenerator.CellSize;
        return x >= minX - margin && x <= maxX + margin &&
               z >= minZ - margin && z <= maxZ + margin;
    }

    private static float NormalizeRotation(float rotation)
    {
        float full = MathF.PI * 2f;
        rotation %= full;
        if (rotation < 0f) rotation += full;
        return rotation;
    }
}
