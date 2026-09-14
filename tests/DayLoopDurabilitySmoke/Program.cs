using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;

const int Seed = 424242;
const string PersistentEventId = "dayloop_persistent_anchor";
const string TemporaryEventId = "dayloop_temporary_event";

string path = Path.Combine(Path.GetTempPath(), $"awakening-dayloop-{Guid.NewGuid():N}.json");
string backup = path + ".bak";

try
{
    var progress = new HeroProgress();
    progress.AddQualities(memory: 17f, curiosity: 23f, empathy: 11f, agency: 19f, courage: 13f);
    Require(progress.RegisterDailyTalk(101) == false, "one daily talk must leave partial daily progress");
    Require(progress.DailyTalkProgress == 1, "precondition: daily objective has progress");

    var persistent = new MemoryEvent(
        PersistentEventId,
        progress.Day,
        "durability",
        "dayloop_test",
        FirstMemorySlice.HeroActorId,
        "make_persistent",
        "persistent event for 20-cycle durability smoke");
    Require(progress.Ledger.TryRecordEvent(persistent), "persistent event must record");
    Require(progress.Ledger.TryCreateAnchor(PersistentEventId, "durability_trace", 2, WitnessConsent.Accepted),
        "persistent event must gain accepted anchor");

    var temporary = new MemoryEvent(
        TemporaryEventId,
        progress.Day,
        "daily_only",
        "dayloop_test",
        FirstMemorySlice.HeroActorId,
        "temporary_choice",
        "unanchored event must disappear on first Sverka");
    Require(progress.Ledger.TryRecordEvent(temporary), "temporary event must record");

    var city = new CityRenderer(Seed, progress);
    ResidentIdentity.BindCity(city.Npcs);
    NpcCharacter awakened = city.Npcs[8];
    NpcCharacter ordinary = city.Npcs[9];
    awakened.Awaken();
    ordinary.Awareness = 55f;
    ordinary.State = NpcState.Working;

    float memory = progress.Memory;
    float curiosity = progress.Curiosity;
    float empathy = progress.Empathy;
    float agency = progress.Agency;
    float courage = progress.Courage;
    int initialDay = progress.Day;

    for (int cycle = 1; cycle <= 20; cycle++)
    {
        progress.NewDay();
        awakened.Reset();
        ordinary.Reset();

        Require(progress.Day == initialDay + cycle, $"day mismatch after cycle {cycle}");
        Require(progress.Ledger.IsPersisted(PersistentEventId), $"persistent anchor lost at cycle {cycle}");
        Require(progress.Ledger.Events.Count == 1 && progress.Ledger.Anchors.Count == 1,
            $"persistent ledger rows duplicated or lost at cycle {cycle}");
        Require(!progress.Ledger.TryGetEvent(TemporaryEventId, out _),
            $"unanchored event survived cycle {cycle}");
        Require(Nearly(progress.Memory, memory) && Nearly(progress.Curiosity, curiosity) &&
                Nearly(progress.Empathy, empathy) && Nearly(progress.Agency, agency) && Nearly(progress.Courage, courage),
            $"persistent hero qualities changed at cycle {cycle}");
        Require(progress.DailyObjectiveDay == progress.Day && progress.DailyTalkProgress == 0 && !progress.DailyObjectiveCompleted,
            $"daily objective leaked across cycle {cycle}");
        Require(awakened.IsAwakened && awakened.State == NpcState.Aware && Nearly(awakened.Awareness, 100f),
            $"awakened NPC lost permanent state at cycle {cycle}");
        Require(!ordinary.IsAwakened && ordinary.State == NpcState.Walking && Nearly(ordinary.Awareness, 0f),
            $"ordinary NPC failed normal reset at cycle {cycle}");

        if (cycle == 10)
        {
            SaveToTemp(path, Seed, progress, city.Npcs);
            var loaded = LoadFromTemp(path);
            var loadedAgain = LoadFromTemp(path);

            Require(loaded.seed == Seed && loaded.progress.Day == progress.Day,
                "mid-series Save/Load changed seed or day");
            Require(loaded.progress.Ledger.Events.Count == 1 && loaded.progress.Ledger.Anchors.Count == 1,
                "mid-series load duplicated or lost persistent ledger rows");
            Require(loadedAgain.progress.Ledger.Events.Count == 1 && loadedAgain.progress.Ledger.Anchors.Count == 1,
                "repeated load duplicated persistent ledger rows");
            Require(Nearly(loaded.progress.Memory, memory) && Nearly(loaded.progress.Curiosity, curiosity) &&
                    Nearly(loaded.progress.Empathy, empathy) && Nearly(loaded.progress.Agency, agency) && Nearly(loaded.progress.Courage, courage),
                "mid-series Save/Load changed persistent hero qualities");

            progress = loaded.progress;
            city = new CityRenderer(Seed, progress);
            city.RestoreNpcs(loaded.npcs);
            ResidentIdentity.BindCity(city.Npcs);
            NpcAwakeningPersistence.RegisterCity(city);
            awakened = city.Npcs[8];
            ordinary = city.Npcs[9];

            Require(awakened.IsAwakened && awakened.State == NpcState.Aware,
                "mid-series Save/Load lost permanent NPC awakening");
        }
    }

    Console.WriteLine("DAYLOOP_DURABILITY_SMOKE=PASS; cycles=20; reload_at=10; anchors=1");
}
catch (Exception e)
{
    Console.Error.WriteLine("DAYLOOP_DURABILITY_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}
finally
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
    try { if (File.Exists(backup)) File.Delete(backup); } catch { }
}

static void SaveToTemp(string path, int seed, HeroProgress progress, IReadOnlyList<NpcCharacter> npcs)
{
    MethodInfo method = typeof(SaveSystem).GetMethod("SaveToPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SaveToPath reflection target missing");
    method.Invoke(null, new object?[]
    {
        path,
        seed,
        progress,
        new AwarenessSystem(),
        10f,
        DateTime.UtcNow,
        npcs,
    });
}

static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<SaveSystem.NpcSaveData>? npcs)
    LoadFromTemp(string path)
{
    MethodInfo method = typeof(SaveSystem).GetMethod("LoadFromPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("LoadFromPath reflection target missing");
    object result = method.Invoke(null, new object?[] { path })
        ?? throw new InvalidOperationException("LoadFromPath returned null");
    return ((int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<SaveSystem.NpcSaveData>? npcs))result;
}

static bool Nearly(float a, float b) => Math.Abs(a - b) <= 0.001f;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
