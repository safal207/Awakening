using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public static class SaveSystem
{
    private static readonly string SaveDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Probuzhdenie");

    private static readonly string SaveFilePath = Path.Combine(SaveDirectory, "save.json");
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions LoadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly UTF8Encoding SaveEncoding = new(false);

    public class NpcSaveData
    {
        public float Friendliness { get; set; }
        public float Trust { get; set; }
        public int TimesTalked { get; set; }
        public float LastTalkDay { get; set; }
        public float Awareness { get; set; }
        public string State { get; set; } = "";
    }

    private class SaveData
    {
        public int Seed { get; set; }
        public int Day { get; set; }
        public float Memory { get; set; }
        public float Curiosity { get; set; }
        public float Empathy { get; set; }
        public float Agency { get; set; }
        public float Courage { get; set; }
        public float Awareness { get; set; }
        public List<string> DiscoveredEggs { get; set; } = new();
        public float TimeOfDay { get; set; }
        public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;
        public List<NpcSaveData>? Npcs { get; set; }
    }

    public static void Save(int seed, HeroProgress progress, AwarenessSystem awareness, float timeOfDay, IReadOnlyList<NpcCharacter>? npcs = null)
    {
        SaveToPath(SaveFilePath, seed, progress, awareness, timeOfDay, DateTime.UtcNow, npcs);
    }

    public static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<NpcSaveData>? npcs) Load()
    {
        return LoadFromPath(SaveFilePath);
    }

    public static bool RunSelfTest(out string message)
    {
        string path = Path.Combine(Path.GetTempPath(), $"probuzhdenie-save-test-{Guid.NewGuid():N}.json");
        try
        {
            var progress = new HeroProgress();
            progress.NewDay();
            progress.DiscoverEgg("self_test", memoryGain: 12f, curiosityGain: 8f, empathyGain: 6f, agencyGain: 4f, courageGain: 2f);

            var awareness = new AwarenessSystem();
            awareness.Restore(85f);

            SaveToPath(path, 424242, progress, awareness, 13.5f, DateTime.UtcNow.AddMinutes(-90), null);
            var loaded = LoadFromPath(path);

            SaveToPath(path, 515151, progress, awareness, 7.25f, DateTime.UtcNow, null);
            var overwritten = LoadFromPath(path);

            bool ok =
                loaded.seed == 424242 &&
                loaded.progress.Day == 2 &&
                Math.Abs(loaded.timeOfDay - 13.5f) < 0.001f &&
                Math.Abs(loaded.awareness - 85f) < 0.001f &&
                loaded.offlineMinutes >= 89 &&
                loaded.progress.DiscoveredEggs.Contains("self_test") &&
                loaded.progress.Memory > progress.Memory &&
                overwritten.seed == 515151 &&
                Math.Abs(overwritten.timeOfDay - 7.25f) < 0.001f;

            message = ok ? "Save/load self-test passed." : "Save/load self-test failed.";
            return ok;
        }
        catch (Exception e)
        {
            message = $"Save/load self-test failed: {e.Message}";
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup for a temp self-test file.
            }
        }
    }

    private static void SaveToPath(string path, int seed, HeroProgress progress, AwarenessSystem awareness, float timeOfDay, DateTime savedUtc, IReadOnlyList<NpcCharacter>? npcs)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var data = new SaveData
            {
                Seed = seed,
                Day = progress.Day,
                Memory = progress.Memory,
                Curiosity = progress.Curiosity,
                Empathy = progress.Empathy,
                Agency = progress.Agency,
                Courage = progress.Courage,
                Awareness = awareness.Level,
                DiscoveredEggs = new List<string>(progress.DiscoveredEggs),
                TimeOfDay = timeOfDay,
                LastSavedUtc = savedUtc,
                Npcs = npcs?.Select(n => new NpcSaveData
                {
                    Friendliness = n.Friendliness,
                    Trust = n.Trust,
                    TimesTalked = n.TimesTalked,
                    LastTalkDay = n.LastTalkDay,
                    Awareness = n.Awareness,
                    State = n.State.ToString(),
                }).ToList(),
            };

            string json = JsonSerializer.Serialize(data, SaveOptions);
            WriteAllTextAtomically(path, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save game: {e}");
        }
    }

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<NpcSaveData>? npcs) LoadFromPath(string path)
    {
        if (!File.Exists(path))
            return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null);

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json, LoadOptions);
            if (data == null) return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null);

            var progress = new HeroProgress();
            progress.Restore(data.Day, data.Memory, data.Curiosity, data.Empathy, data.Agency, data.Courage);
            progress.LoadDiscoveredEggs(data.DiscoveredEggs);

            double minutesAway = Math.Max(0d, (DateTime.UtcNow - data.LastSavedUtc.ToUniversalTime()).TotalMinutes);
            double offlineMinutes = data.Awareness >= HeroProgress.OfflineGrowthAwarenessThreshold
                ? progress.ApplyOfflineGrowth(minutesAway)
                : 0d;

            return (data.Seed == 0 ? Environment.TickCount : data.Seed, progress, data.TimeOfDay, data.Awareness, offlineMinutes, data.Npcs);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load game: {e}");
            return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null);
        }
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, contents, SaveEncoding);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // A stale temp file is better than risking the active save.
            }
        }
    }
}
