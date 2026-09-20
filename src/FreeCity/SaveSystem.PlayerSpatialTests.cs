using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private static bool RunPlayerSpatialPersistenceSelfTest(out string message)
    {
        var paths = new List<string>();
        try
        {
            CheckExteriorSpatialRoundTrip(paths);
            CheckInteriorSpatialResolution(paths);
            CheckLegacySaveWithoutSpatialState(paths);
            CheckCorruptSpatialStateKeepsSafeSpawn(paths);
            message = "Player spatial persistence self-test passed (4 cases).";
            return true;
        }
        catch (Exception e)
        {
            message = "Player spatial persistence self-test failed: " + e.Message;
            return false;
        }
        finally
        {
            foreach (string path in paths)
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }
    }

    private static void CheckExteriorSpatialRoundTrip(List<string> paths)
    {
        string path = TempSavePath(paths, "player-exterior");
        const int seed = 616161;
        var progress = new HeroProgress();
        var city = new CityRenderer(seed, progress);
        ResidentIdentity.BindCity(city.Npcs);
        PlayerSpatialPersistence.RegisterCity(city);

        Vector3 savedPosition = city.ClampToWalkable(new Vector3(-1f, 0f, 5f), 0.3f);
        city.Player!.Position = savedPosition;
        city.Player.Rotation = 1.25f;
        city.Player.TargetRotation = 1.25f;

        SaveToPath(path, seed, progress, new AwarenessSystem(), 14f, DateTime.UtcNow, city.Npcs);
        var loaded = LoadFromPath(path);
        var restoredCity = new CityRenderer(loaded.seed, loaded.progress);
        ResidentIdentity.BindCity(restoredCity.Npcs);
        PlayerSpatialPersistence.RegisterCity(restoredCity);

        RequireMemory(!restoredCity.IsInside, "exterior round-trip must stay outside");
        RequireMemory(Vector3.DistanceSquared(restoredCity.Player!.Position, savedPosition) < 0.0001f,
            "exterior position must survive save/load");
        RequireMemory(Math.Abs(restoredCity.Player.Rotation - 1.25f) < 0.0001f,
            "player rotation must survive save/load");
        RequireMemory(!loaded.progress.SaveWritesBlocked,
            "valid exterior spatial state must remain writable");
    }

    private static void CheckInteriorSpatialResolution(List<string> paths)
    {
        string path = TempSavePath(paths, "player-interior");
        const int seed = 626262;
        var probeCity = new CityRenderer(seed, new HeroProgress());
        CityBlock block = probeCity.Blocks.First(b => b.Type is not (BuildingType.Tree or BuildingType.Lamp));

        var data = BaseSaveData();
        data.Seed = seed;
        data.PlayerSpatial = new PlayerSpatialSaveData
        {
            X = block.X + block.Width * 0.65f,
            Y = 0f,
            Z = block.Z + block.Depth * 0.55f,
            Rotation = -0.75f,
            IsInside = true,
            InteriorBlockX = block.X,
            InteriorBlockZ = block.Z,
        };
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        RequireMemory(PlayerSpatialPersistence.TryGetPending(loaded.progress, out PlayerSpatialSaveData? pending) && pending != null,
            "interior spatial row must become pending restore data");

        var restoreCity = new CityRenderer(seed, loaded.progress);
        RequireMemory(PlayerSpatialPersistence.TryResolve(restoreCity, pending, out ResolvedPlayerSpatial resolved),
            "valid interior spatial row must resolve without OpenGL");
        RequireMemory(resolved.InteriorBlock is CityBlock restoredBlock &&
                      restoredBlock.X == block.X && restoredBlock.Z == block.Z,
            "interior restore must resolve the same deterministic city block");
        RequireMemory(resolved.Position.X > block.X && resolved.Position.X < block.X + block.Width &&
                      resolved.Position.Z > block.Z && resolved.Position.Z < block.Z + block.Depth,
            "interior restore must clamp to a safe point inside the same block");
        RequireMemory(resolved.Rotation >= 0f && resolved.Rotation < MathF.PI * 2f,
            "restored rotation must be normalized");
    }

    private static void CheckLegacySaveWithoutSpatialState(List<string> paths)
    {
        string path = TempSavePath(paths, "player-legacy");
        var data = BaseSaveData();
        data.Seed = 636363;
        data.PlayerSpatial = null;
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        RequireMemory(!PlayerSpatialPersistence.TryGetPending(loaded.progress, out _),
            "save without PlayerSpatial must keep legacy spawn behavior");
        RequireMemory(!loaded.progress.SaveWritesBlocked,
            "legacy save without PlayerSpatial must remain writable");
    }

    private static void CheckCorruptSpatialStateKeepsSafeSpawn(List<string> paths)
    {
        string path = TempSavePath(paths, "player-corrupt");
        const int seed = 646464;
        var data = BaseSaveData();
        data.Seed = seed;
        data.PlayerSpatial = new PlayerSpatialSaveData
        {
            X = 0f,
            Y = 0f,
            Z = 0f,
            Rotation = 0f,
            IsInside = true,
            InteriorBlockX = 999999,
            InteriorBlockZ = 999999,
        };
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        var city = new CityRenderer(seed, loaded.progress);
        Vector3 safeSpawn = city.Player!.Position;
        PlayerSpatialPersistence.RegisterCity(city);

        RequireMemory(Vector3.DistanceSquared(city.Player.Position, safeSpawn) < 0.0001f,
            "invalid interior row must keep the safe spawn");
        RequireMemory(!city.IsInside, "invalid interior row must not enter a building");
        RequireMemory(loaded.progress.SaveWritesBlocked,
            "invalid spatial row must protect the source save from automatic overwrite");
    }
}
