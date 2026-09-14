using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private const int CurrentSchemaVersion = 2;

    private static readonly string SaveDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Probuzhdenie");

    private static readonly string SaveFilePath = Path.Combine(SaveDirectory, "save.json");
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions LoadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly UTF8Encoding SaveEncoding = new(false);

    public class NpcSaveData
    {
        // World-local stable identity. Null means a legacy pre-PersistentId save.
        public int? PersistentId { get; set; }
        // Nullable keeps legacy rows distinguishable from explicit modern state.
        public bool? IsAwakened { get; set; }
        public float Friendliness { get; set; }
        public float Trust { get; set; }
        public int TimesTalked { get; set; }
        public float LastTalkDay { get; set; }
        public float Awareness { get; set; }
        public string State { get; set; } = "";
    }

    private class SaveData
    {
        public int SchemaVersion { get; set; }
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
        public int DailyObjectiveDay { get; set; } = 1;
        public int DailyTalkProgress { get; set; }
        public bool DailyObjectiveCompleted { get; set; }
        public List<int>? DailyTalkedNpcs { get; set; }
        public MemoryPersistenceSnapshot? MemoryLedger { get; set; }
        public PlayerSpatialSaveData? PlayerSpatial { get; set; }
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

            bool coreOk =
                loaded.seed == 424242 &&
                loaded.progress.Day == 2 &&
                Math.Abs(loaded.timeOfDay - 13.5f) < 0.001f &&
                Math.Abs(loaded.awareness - 85f) < 0.001f &&
                loaded.offlineMinutes >= 89 &&
                loaded.progress.DiscoveredEggs.Contains("self_test") &&
                loaded.progress.Memory > progress.Memory &&
                overwritten.seed == 515151 &&
                Math.Abs(overwritten.timeOfDay - 7.25f) < 0.001f;

            bool memoryOk = RunMemoryPersistenceSelfTest(out string memoryMessage);
            bool spatialOk = RunPlayerSpatialPersistenceSelfTest(out string spatialMessage);
            bool recoveryOk = RunSaveRecoverySelfTest(out string recoveryMessage);
            bool npcAwakeningOk = RunNpcAwakeningPersistenceSelfTest(out string npcAwakeningMessage);
            bool ok = coreOk && memoryOk && spatialOk && recoveryOk && npcAwakeningOk;
            message = ok
                ? $"Save/load self-test passed. {memoryMessage} {spatialMessage} {recoveryMessage} {npcAwakeningMessage}"
                : $"Save/load self-test failed. Core={coreOk}; {memoryMessage} {spatialMessage} {recoveryMessage} {npcAwakeningMessage}";
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
                string backup = BackupPathFor(path);
                if (File.Exists(backup)) File.Delete(backup);
            }
            catch
            {
                // Best-effort cleanup for temp self-test files.
            }
        }
    }

    private static void SaveToPath(string path, int seed, HeroProgress progress, AwarenessSystem awareness, float timeOfDay, DateTime savedUtc, IReadOnlyList<NpcCharacter>? npcs)
    {
        if (progress.SaveWritesBlocked)
        {
            Console.WriteLine("Save skipped: loaded save requires recovery acknowledgement before it can be overwritten.");
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var data = new SaveData
            {
                SchemaVersion = CurrentSchemaVersion,
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
                Npcs = npcs?.Select((n, persistentId) => new NpcSaveData
                {
                    PersistentId = persistentId,
                    IsAwakened = n.IsAwakened,
                    Friendliness = n.Friendliness,
                    Trust = n.Trust,
                    TimesTalked = n.TimesTalked,
                    LastTalkDay = n.LastTalkDay,
                    Awareness = n.Awareness,
                    State = n.State.ToString(),
                }).ToList(),
                DailyObjectiveDay = progress.DailyObjectiveDay,
                DailyTalkProgress = progress.DailyTalkProgress,
                DailyObjectiveCompleted = progress.DailyObjectiveCompleted,
                DailyTalkedNpcs = new List<int>(progress.DailyTalkedNpcs),
                MemoryLedger = MemoryPersistence.Capture(progress.Ledger),
                PlayerSpatial = PlayerSpatialPersistence.Capture(npcs),
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
        return LoadFromPathWithRecovery(path);
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        string backupPath = BackupPathFor(path);
        string backupTempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.backup.tmp");
        bool stageBackup = File.Exists(path) && IsFullyValidSaveFile(path);

        try
        {
            File.WriteAllText(tempPath, contents, SaveEncoding);

            if (stageBackup)
                File.Copy(path, backupTempPath, overwrite: true);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);

            if (stageBackup && File.Exists(backupTempPath))
            {
                try
                {
                    File.Move(backupTempPath, backupPath, overwrite: true);
                }
                catch (Exception e)
                {
                    // Primary save already succeeded. Keep any older valid backup
                    // rather than turning backup promotion into a failed game save.
                    Console.WriteLine($"Primary save succeeded, but backup promotion failed: {e.Message}");
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(backupTempPath)) File.Delete(backupTempPath);
            }
            catch
            {
                // A stale temp file is better than risking the active save.
            }
        }
    }
}
