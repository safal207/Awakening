using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Probuzhdenie.FreeCity;

public static class SaveSystem
{
    private const int CurrentSaveVersion = 2;
    private static readonly string SaveDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Probuzhdenie");
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
        public int Version { get; set; }
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
        public List<MemoryEvent> MemoryEvents { get; set; } = new();
        public List<MemoryAnchorSaveData> MemoryAnchors { get; set; } = new();
        public List<string> RewardedDialogueChoices { get; set; } = new();
    }

    public static void Save(int seed, HeroProgress progress, AwarenessSystem awareness, float timeOfDay,
        IReadOnlyList<NpcCharacter>? npcs = null, MemoryLedger? memoryLedger = null)
    {
        SaveToPath(SaveFilePath, seed, progress, awareness, timeOfDay, DateTime.UtcNow, npcs,
            memoryLedger ?? MemoryRuntime.Current);
    }

    public static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes,
        List<NpcSaveData>? npcs, MemoryLedger memoryLedger) Load()
    {
        var loaded = LoadFromPath(SaveFilePath);
        MemoryRuntime.Replace(loaded.memoryLedger);
        return loaded;
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

            var ledger = new MemoryLedger();
            var memoryEvent = new MemoryEvent
            {
                EventId = "save-self-test",
                EventType = "shared_meeting",
                ParticipantIds = new List<int> { 0, 12 },
                LocationId = "test-location",
                ChoiceId = "test-choice",
                Day = 2,
                Consequence = "test consequence",
                HadAlternativeChoice = true,
            };
            var anchor = new MemoryAnchor
            {
                EventId = memoryEvent.EventId,
                TraceId = "test-trace",
                Witnesses = new List<MemoryWitness>
                {
                    new() { NpcId = 12, UnderstoodEvent = true, Consented = true },
                },
            };
            ledger.RegisterEvent(memoryEvent);
            ledger.RegisterAnchor(anchor);
            ledger.ResolveForSverka(memoryEvent.EventId, 0, out _);

            SaveToPath(path, 424242, progress, awareness, 13.5f, DateTime.UtcNow.AddMinutes(-90), null, ledger);
            var loaded = LoadFromPath(path);
            SaveToPath(path, 515151, progress, awareness, 7.25f, DateTime.UtcNow, null, loaded.memoryLedger);
            var overwritten = LoadFromPath(path);

            bool ok =
                loaded.seed == 424242 &&
                loaded.progress.Day == 2 &&
                Math.Abs(loaded.timeOfDay - 13.5f) < 0.001f &&
                Math.Abs(loaded.awareness - 85f) < 0.001f &&
                loaded.offlineMinutes >= 89 &&
                loaded.progress.DiscoveredEggs.Contains("self_test") &&
                loaded.progress.Memory > progress.Memory &&
                loaded.memoryLedger.HasPersisted("save-self-test") &&
                overwritten.seed == 515151 &&
                overwritten.memoryLedger.HasPersisted("save-self-test") &&
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
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    public static bool RunMemoryRoundTripTest(out string message)
    {
        string path = Path.Combine(Path.GetTempPath(), $"awakening-memory-test-{Guid.NewGuid():N}.json");
        var previousLedger = MemoryRuntime.Current;
        int previousHero = MemoryRuntime.HeroId;
        void Require(bool ok, string detail) { if (!ok) throw new InvalidOperationException(detail); }
        try
        {
            foreach (bool consent in new[] { true, false })
            {
                MemoryRuntime.Reset();
                var progress = new HeroProgress();
                var city = new CityRenderer(424242, progress);
                var lida = city.Npcs[1];
                var mark = city.Npcs[2];
                if (!consent) mark.Trust = 0f;
                FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var choices);
                var lidaChoice = choices[0];
                Require(lida.ApplyChoice(lidaChoice, progress), "Lida creates candidate");

                SaveToPath(path, 424242, progress, city.Awareness, 13f, DateTime.UtcNow, city.Npcs, MemoryRuntime.Current);
                var loaded = LoadFromPath(path);
                Require(loaded.memoryLedger.FindAnchor(FirstDistrictStory.MeetingEventId)?.Status == MemoryAnchorStatus.Candidate,
                    "candidate survives save before witness decision");
                MemoryRuntime.Replace(loaded.memoryLedger);
                progress = loaded.progress;
                city = new CityRenderer(loaded.seed, progress);
                city.RestoreNpcs(loaded.npcs);
                mark = city.Npcs[2];
                Require(!city.Npcs[1].ApplyChoice(lidaChoice, progress), "Lida reward not replayed after load");
                FirstDistrictStory.TryGetDialogue(mark, progress, out _, out choices);
                var witnessChoice = choices[0];
                Require(mark.ApplyChoice(witnessChoice, progress), "Mark decides after load");

                SaveToPath(path, 424242, progress, city.Awareness, 23.9f, DateTime.UtcNow, city.Npcs, MemoryRuntime.Current);
                loaded = LoadFromPath(path);
                MemoryRuntime.Replace(loaded.memoryLedger);
                progress = loaded.progress;
                city = new CityRenderer(loaded.seed, progress);
                city.RestoreNpcs(loaded.npcs);
                city.TimeOfDay = 24f;
                city.AdvanceDayClock(0.1f);
                Require(progress.Day == 2, "loaded world crosses midnight");
                Require(loaded.memoryLedger.HasPersisted(FirstDistrictStory.MeetingEventId) == consent, "correct morning outcome");
                string reason = loaded.memoryLedger.FindAnchor(FirstDistrictStory.MeetingEventId)!.DecisionReason;

                for (int morning = 0; morning < 20; morning++) progress.NewDay();
                SaveToPath(path, 424242, progress, city.Awareness, 8f, DateTime.UtcNow, city.Npcs, loaded.memoryLedger);
                var restored = LoadFromPath(path);
                MemoryRuntime.Replace(restored.memoryLedger);
                var restoredCity = new CityRenderer(restored.seed, restored.progress);
                restoredCity.RestoreNpcs(restored.npcs);
                var before = (restored.progress.Memory, restored.progress.Empathy, restored.progress.Agency);
                Require(!restoredCity.Npcs[2].ApplyChoice(witnessChoice, restored.progress), "witness reward not replayed after 20 mornings and load");
                Require(before == (restored.progress.Memory, restored.progress.Empathy, restored.progress.Agency), "qualities unchanged on replay");
                Require(restored.memoryLedger.Events.Count == 1 && restored.memoryLedger.Anchors.Count == 1,
                    "one event and anchor across save and reset");
                Require(restored.memoryLedger.HasPersisted(FirstDistrictStory.MeetingEventId) == consent &&
                    restored.memoryLedger.FindAnchor(FirstDistrictStory.MeetingEventId)!.DecisionReason == reason,
                    "preserve final decision and causal reason");
            }

            foreach (string legacy in new[] { "{\"Seed\":424242,\"Day\":1}", "{\"Version\":1,\"Seed\":424242,\"Day\":1}" })
            {
                File.WriteAllText(path, legacy, SaveEncoding);
                var loaded = LoadFromPath(path);
                Require(loaded.seed == 424242 && loaded.memoryLedger.Events.Count == 0 &&
                    loaded.progress.RewardedDialogueChoices.Count == 0, "legacy save defaults");
            }
            File.WriteAllText(path, JsonSerializer.Serialize(new { Version = CurrentSaveVersion + 1 }), SaveEncoding);
            bool refusedFutureVersion = false;
            try { LoadFromPath(path); }
            catch (InvalidDataException) { refusedFutureVersion = true; }
            Require(refusedFutureVersion, "do not overwrite a newer save schema");
            message = "Memory save/load tests passed (both outcomes, 20 mornings, replay and legacy saves).";
            return true;
        }
        catch (Exception e)
        {
            message = "Memory save/load tests failed: " + e.Message;
            return false;
        }
        finally
        {
            MemoryRuntime.Replace(previousLedger);
            MemoryRuntime.HeroId = previousHero;
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    private static void SaveToPath(string path, int seed, HeroProgress progress, AwarenessSystem awareness,
        float timeOfDay, DateTime savedUtc, IReadOnlyList<NpcCharacter>? npcs, MemoryLedger? memoryLedger = null)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var data = new SaveData
            {
                Version = CurrentSaveVersion,
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
                DailyObjectiveDay = progress.DailyObjectiveDay,
                DailyTalkProgress = progress.DailyTalkProgress,
                DailyObjectiveCompleted = progress.DailyObjectiveCompleted,
                DailyTalkedNpcs = new List<int>(progress.DailyTalkedNpcs),
                MemoryEvents = memoryLedger?.Events.ToList() ?? new List<MemoryEvent>(),
                MemoryAnchors = memoryLedger?.Anchors.Select(MemoryAnchorSaveData.From).ToList() ?? new List<MemoryAnchorSaveData>(),
                RewardedDialogueChoices = progress.RewardedDialogueChoices.ToList(),
            };

            string json = JsonSerializer.Serialize(data, SaveOptions);
            WriteAllTextAtomically(path, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save game: {e}");
        }
    }

    private static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes,
        List<NpcSaveData>? npcs, MemoryLedger memoryLedger) LoadFromPath(string path)
    {
        if (!File.Exists(path))
            return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null, new MemoryLedger());

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json, LoadOptions);
            if (data == null)
                return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null, new MemoryLedger());
            if (data.Version > CurrentSaveVersion)
                throw new InvalidDataException($"Save version {data.Version} is newer than supported version {CurrentSaveVersion}.");

            var progress = new HeroProgress();
            progress.Restore(data.Day, data.Memory, data.Curiosity, data.Empathy, data.Agency, data.Courage);
            progress.LoadDiscoveredEggs(data.DiscoveredEggs);
            progress.LoadDialogueRewards(data.RewardedDialogueChoices);
            progress.LoadDailyObjective(data.DailyObjectiveDay, data.DailyTalkProgress, data.DailyObjectiveCompleted, data.DailyTalkedNpcs);

            var ledger = new MemoryLedger();
            var anchors = (data.MemoryAnchors ?? new List<MemoryAnchorSaveData>()).Select(a => a.ToAnchor()).ToList();
            ledger.Restore(data.MemoryEvents, anchors);

            double minutesAway = Math.Max(0d, (DateTime.UtcNow - data.LastSavedUtc.ToUniversalTime()).TotalMinutes);
            double offlineMinutes = data.Awareness >= HeroProgress.OfflineGrowthAwarenessThreshold
                ? progress.ApplyOfflineGrowth(minutesAway)
                : 0d;

            return (data.Seed == 0 ? Environment.TickCount : data.Seed, progress, data.TimeOfDay,
                data.Awareness, offlineMinutes, data.Npcs, ledger);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load game: {e}");
            return (Environment.TickCount, new HeroProgress(), 8f, 0f, 0d, null, new MemoryLedger());
        }
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, contents, SaveEncoding);
            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { }
        }
    }
}
