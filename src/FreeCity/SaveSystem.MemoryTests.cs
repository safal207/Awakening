using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    private static bool RunMemoryPersistenceSelfTest(out string message)
    {
        var paths = new List<string>();
        try
        {
            CheckMemoryRoundTrip(paths);
            CheckOldSaveMigration(paths);
            CheckMissingEventAnchorRecovery(paths);
            CheckDeclinedWitnessRemainsNonPersistent(paths);
            CheckCorruptIdDoesNotCrash(paths);
            CheckFutureSchemaBlocksOverwrite(paths);
            message = "Memory persistence self-test passed (6 cases).";
            return true;
        }
        catch (Exception e)
        {
            message = "Memory persistence self-test failed: " + e.Message;
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

    private static void CheckMemoryRoundTrip(List<string> paths)
    {
        string path = TempSavePath(paths, "roundtrip");
        var progress = new HeroProgress();
        progress.NewDay();
        var memoryEvent = new MemoryEvent(
            FirstMemorySlice.EventId,
            progress.Day,
            "shared_meeting",
            FirstMemorySlice.LocationId,
            FirstMemorySlice.HeroActorId,
            FirstMemorySlice.HelpLidaActionId,
            "Lida delays the tram and meets Mark in the plaza.");
        RequireMemory(progress.Ledger.TryRecordEvent(memoryEvent), "round-trip event should record");
        RequireMemory(progress.Ledger.TryCreateAnchor(
            memoryEvent.Id, FirstMemorySlice.TraceId, 12, WitnessConsent.Accepted),
            "round-trip anchor should record");

        float memoryBefore = progress.Memory;
        var awareness = new AwarenessSystem();
        SaveToPath(path, 424242, progress, awareness, 10.5f, DateTime.UtcNow, null);

        var loaded = LoadFromPath(path);
        RequireMemory(!loaded.progress.SaveWritesBlocked, "clean v2 save should remain writable");
        RequireMemory(loaded.progress.Memory == memoryBefore, "loading memory ledger must not grant quality rewards");
        RequireMemory(loaded.progress.Ledger.Events.Count == 1 && loaded.progress.Ledger.Anchors.Count == 1,
            "round-trip must restore one event and one anchor");
        RequireMemory(loaded.progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
            "accepted anchor must remain persisted after load");
        RequireMemory(loaded.progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out var restored) &&
            restored != null && restored.LocationId == FirstMemorySlice.LocationId &&
            restored.ChoiceId == FirstMemorySlice.HelpLidaActionId,
            "round-trip must preserve location and causal choice");

        var loadedAgain = LoadFromPath(path);
        RequireMemory(loadedAgain.progress.Ledger.Events.Count == 1 && loadedAgain.progress.Ledger.Anchors.Count == 1,
            "repeated load must not duplicate memory rows");

        loaded.progress.NewDay();
        RequireMemory(loaded.progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
            "anchored event must survive next-day Sverka");
    }

    private static void CheckOldSaveMigration(List<string> paths)
    {
        string path = TempSavePath(paths, "legacy");
        var oldSave = new
        {
            Seed = 111,
            Day = 3,
            Memory = 4f,
            Curiosity = 5f,
            Empathy = 6f,
            Agency = 7f,
            Courage = 8f,
            Awareness = 0f,
            DiscoveredEggs = Array.Empty<string>(),
            TimeOfDay = 9f,
            LastSavedUtc = DateTime.UtcNow,
            DailyObjectiveDay = 3,
            DailyTalkProgress = 0,
            DailyObjectiveCompleted = false,
            DailyTalkedNpcs = Array.Empty<int>(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(oldSave, SaveOptions));

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.seed == 111 && loaded.progress.Day == 3, "legacy core save should load");
        RequireMemory(loaded.progress.Ledger.Events.Count == 0 && loaded.progress.Ledger.Anchors.Count == 0,
            "legacy save should migrate with an empty ledger");
        RequireMemory(!loaded.progress.SaveWritesBlocked, "legacy save should remain writable after migration");
    }

    private static void CheckMissingEventAnchorRecovery(List<string> paths)
    {
        string path = TempSavePath(paths, "missing-event");
        var data = BaseSaveData();
        data.MemoryLedger = new MemoryPersistenceSnapshot
        {
            Anchors = new List<MemoryAnchorSaveData>
            {
                new()
                {
                    EventId = "missing",
                    TraceId = "trace",
                    CreatedDay = 1,
                    Witnesses = new List<MemoryWitnessSaveData>
                    {
                        new() { NpcId = 5, Consent = nameof(WitnessConsent.Accepted) }
                    }
                }
            }
        };
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.progress.Ledger.Anchors.Count == 0, "anchor without event must be ignored");
        RequireMemory(loaded.progress.SaveWritesBlocked,
            "partially recovered memory data must block automatic overwrite");
    }

    private static void CheckDeclinedWitnessRemainsNonPersistent(List<string> paths)
    {
        string path = TempSavePath(paths, "declined");
        var data = BaseSaveData();
        data.MemoryLedger = SnapshotWithWitness(WitnessConsent.Declined);
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        RequireMemory(!loaded.progress.SaveWritesBlocked, "valid declined consent is not corruption");
        RequireMemory(!loaded.progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
            "declined witness must never become persisted memory");
        loaded.progress.NewDay();
        RequireMemory(!loaded.progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
            "declined event must be forgotten by Sverka");
    }

    private static void CheckCorruptIdDoesNotCrash(List<string> paths)
    {
        string path = TempSavePath(paths, "corrupt-id");
        var data = BaseSaveData();
        data.MemoryLedger = new MemoryPersistenceSnapshot
        {
            Events = new List<MemoryEventSaveData>
            {
                EventRow(FirstMemorySlice.EventId),
                EventRow(""),
            }
        };
        WriteSaveData(path, data);

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
            "valid rows should survive partial recovery");
        RequireMemory(loaded.progress.SaveWritesBlocked,
            "corrupt stable ID must not crash and must protect source save from overwrite");
    }

    private static void CheckFutureSchemaBlocksOverwrite(List<string> paths)
    {
        string path = TempSavePath(paths, "future-schema");
        var data = BaseSaveData();
        data.SchemaVersion = CurrentSchemaVersion + 10;
        WriteSaveData(path, data);
        string before = File.ReadAllText(path);

        var loaded = LoadFromPath(path);
        RequireMemory(loaded.progress.SaveWritesBlocked, "future save schema must block writes");
        SaveToPath(path, 999, loaded.progress, new AwarenessSystem(), 12f, DateTime.UtcNow, null);
        RequireMemory(File.ReadAllText(path) == before, "blocked save must not overwrite future schema file");
    }

    private static SaveData BaseSaveData() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Seed = 222,
        Day = 1,
        Awareness = 0f,
        DiscoveredEggs = new List<string>(),
        TimeOfDay = 8f,
        LastSavedUtc = DateTime.UtcNow,
        DailyObjectiveDay = 1,
        DailyTalkedNpcs = new List<int>(),
        MemoryLedger = new MemoryPersistenceSnapshot(),
    };

    private static MemoryPersistenceSnapshot SnapshotWithWitness(WitnessConsent consent) => new()
    {
        Events = new List<MemoryEventSaveData> { EventRow(FirstMemorySlice.EventId) },
        Anchors = new List<MemoryAnchorSaveData>
        {
            new()
            {
                EventId = FirstMemorySlice.EventId,
                TraceId = FirstMemorySlice.TraceId,
                CreatedDay = 1,
                Witnesses = new List<MemoryWitnessSaveData>
                {
                    new() { NpcId = 12, Consent = consent.ToString() }
                }
            }
        }
    };

    private static MemoryEventSaveData EventRow(string id) => new()
    {
        Id = id,
        Day = 1,
        Kind = "shared_meeting",
        LocationId = FirstMemorySlice.LocationId,
        ActorId = FirstMemorySlice.HeroActorId,
        ChoiceId = FirstMemorySlice.HelpLidaActionId,
        Description = "meeting",
    };

    private static void WriteSaveData(string path, SaveData data) =>
        File.WriteAllText(path, JsonSerializer.Serialize(data, SaveOptions));

    private static string TempSavePath(List<string> paths, string name)
    {
        string path = Path.Combine(Path.GetTempPath(), $"probuzhdenie-{name}-{Guid.NewGuid():N}.json");
        paths.Add(path);
        return path;
    }

    private static void RequireMemory(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
