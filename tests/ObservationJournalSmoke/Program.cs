using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

string meetingPath = TempPath("meeting");
string repairPath = TempPath("repair");

try
{
    int meeting = RunMeeting(meetingPath);
    int repair = RunRepair(repairPath);
    int refusal = RunRefusal();

    Console.WriteLine(
        $"OBSERVATION_JOURNAL_SMOKE=PASS; meeting={meeting}; repair={repair}; refusal={refusal}; duplicates=0");
}
catch (Exception e)
{
    Console.Error.WriteLine("OBSERVATION_JOURNAL_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}
finally
{
    Cleanup(meetingPath);
    Cleanup(repairPath);
}

static int RunMeeting(string path)
{
    HeroProgress progress = new();
    World world = BuildWorld(425001, progress, null);

    DialogueChoice inspect = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.InspectSignalActionId);
    world.Lida.ApplyChoice(inspect, progress);

    ObservationJournal journal = ObservationJournalState.For(progress);
    Require(journal.Entries.Count == 3, "meeting: investigation should create fact/question/promise");
    RequireNoDuplicates(journal, "meeting after investigation");
    RequireStatus(journal, FirstMemoryJournal.RepairPromiseId, ObservationStatus.Open);

    // Replaying the same semantic action must not duplicate stable journal entries.
    FirstMemorySlice.ApplyChoice(world.Lida, inspect, progress);
    Require(journal.Entries.Count == 3, "meeting: duplicate investigation must be idempotent");

    (progress, world) = SaveReload(path, 425001, progress, world.City.Npcs);
    journal = ObservationJournalState.For(progress);
    Require(journal.Entries.Count == 3, "meeting: journal must survive morning-1 Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    DialogueChoice help = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.HelpLidaActionId);
    world.Lida.ApplyChoice(help, progress);

    journal = ObservationJournalState.For(progress);
    RequireStatus(journal, FirstMemoryJournal.RepairPromiseId, ObservationStatus.NotCompleted);
    RequireStatus(journal, FirstMemoryJournal.MeetingId, ObservationStatus.Open);

    DialogueChoice accept = FindAction(
        world.Mark.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.AcceptWitnessActionId);
    world.Mark.ApplyChoice(accept, progress);

    RequireStatus(journal, FirstMemoryJournal.MeetingId, ObservationStatus.Completed);
    RequireStatus(journal, FirstMemoryJournal.WitnessFactId, ObservationStatus.Completed);
    Require(journal.Entries.Count == 5, "meeting: accepted witness should produce exactly five entries before archive");

    (progress, world) = SaveReload(path, 425001, progress, world.City.Npcs);
    journal = ObservationJournalState.For(progress);
    Require(journal.Entries.Count == 5, "meeting: branch journal must survive second Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    DialogueChoice archive = FindAction(
        world.Nika.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.NikaArchiveActionId);
    world.Nika.ApplyChoice(archive, progress);

    journal = ObservationJournalState.For(progress);
    RequireStatus(journal, FirstMemoryJournal.ArchiveFactId, ObservationStatus.Completed);
    Require(journal.Entries.Count == 6, "meeting: Nika archive should add one factual entry");
    RequireNoDuplicates(journal, "meeting final");
    return journal.Entries.Count;
}

static int RunRepair(string path)
{
    HeroProgress progress = new();
    World world = BuildWorld(425002, progress, null);

    DialogueChoice routine = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.RoutineRepairDayOneActionId);
    world.Lida.ApplyChoice(routine, progress);

    ObservationJournal journal = ObservationJournalState.For(progress);
    Require(journal.Entries.Count == 2, "repair: routine first morning should record fact + question");
    RequireStatus(journal, FirstMemoryJournal.RoutineRepairFactId, ObservationStatus.Completed);

    (progress, world) = SaveReload(path, 425002, progress, world.City.Npcs);
    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);

    DialogueChoice inspect = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.InspectSignalActionId);
    world.Lida.ApplyChoice(inspect, progress);

    DialogueChoice repair = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.RepairPromiseActionId);
    world.Lida.ApplyChoice(repair, progress);

    journal = ObservationJournalState.For(progress);
    RequireStatus(journal, FirstMemoryJournal.RepairPromiseId, ObservationStatus.Completed);
    RequireStatus(journal, FirstMemoryJournal.RepairFactId, ObservationStatus.Completed);
    Require(!journal.TryGet(FirstMemoryJournal.MeetingId, out _),
        "repair: journal must not invent a Lida/Mark meeting");
    Require(journal.Entries.Count == 5, "repair: expected five stable observations");

    (progress, world) = SaveReload(path, 425002, progress, world.City.Npcs);
    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);

    journal = ObservationJournalState.For(progress);
    Require(journal.Entries.Count == 5, "repair: journal must survive outcome morning Load/Sverka");
    RequireNoDuplicates(journal, "repair final");
    return journal.Entries.Count;
}

static int RunRefusal()
{
    HeroProgress progress = new();
    World world = BuildWorld(425003, progress, null);

    world.Lida.ApplyChoice(
        FindAction(world.Lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.InspectSignalActionId),
        progress);
    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);

    world.Lida.ApplyChoice(
        FindAction(world.Lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId),
        progress);

    DialogueChoice decline = FindAction(
        world.Mark.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.DeclineWitnessActionId);
    world.Mark.ApplyChoice(decline, progress);

    ObservationJournal journal = ObservationJournalState.For(progress);
    RequireStatus(journal, FirstMemoryJournal.MeetingId, ObservationStatus.NotCompleted);
    RequireStatus(journal, FirstMemoryJournal.WitnessFactId, ObservationStatus.NotCompleted);
    Require(!progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "refusal: journal fact must not secretly create an anchor");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "refusal: unanchored event must still be forgotten by Sverka");
    Require(!journal.TryGet(FirstMemoryJournal.ArchiveFactId, out _),
        "refusal: journal must not claim Nika archived an unconsented meeting");
    Require(journal.Entries.Count == 5, "refusal: expected five stable observations");
    RequireNoDuplicates(journal, "refusal final");
    return journal.Entries.Count;
}

static World BuildWorld(int seed, HeroProgress progress, List<SaveSystem.NpcSaveData>? npcRows)
{
    var city = new CityRenderer(seed, progress);
    city.RestoreNpcs(npcRows);
    var detector = new InteractionDetector(city);
    return new World(city, detector, city.Npcs[1], city.Npcs[2], city.Npcs[3]);
}

static (HeroProgress progress, World world) SaveReload(
    string path,
    int seed,
    HeroProgress progress,
    IReadOnlyList<NpcCharacter> npcs)
{
    SaveToTemp(path, seed, progress, npcs);
    var loaded = LoadFromTemp(path);
    Require(!loaded.progress.SaveWritesBlocked, "journal Save/Load must remain clean");
    return (loaded.progress, BuildWorld(seed, loaded.progress, loaded.npcs));
}

static void SaveToTemp(string path, int seed, HeroProgress progress, IReadOnlyList<NpcCharacter> npcs)
{
    MethodInfo method = typeof(SaveSystem).GetMethod("SaveToPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SaveToPath reflection target missing");
    method.Invoke(null, new object?[]
    {
        path, seed, progress, new AwarenessSystem(), 10f, DateTime.UtcNow, npcs,
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

static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
{
    foreach (DialogueChoice choice in choices)
        if (choice.ActionId == actionId) return choice;
    throw new InvalidOperationException("Missing action: " + actionId);
}

static void RequireStatus(ObservationJournal journal, string id, ObservationStatus expected)
{
    Require(journal.TryGet(id, out ObservationEntry? entry) && entry != null,
        "missing journal entry: " + id);
    Require(entry!.Status == expected,
        $"journal entry {id} expected {expected}, got {entry.Status}");
}

static void RequireNoDuplicates(ObservationJournal journal, string label)
{
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (ObservationEntry entry in journal.Entries)
        Require(ids.Add(entry.Id), label + ": duplicate journal id " + entry.Id);
}

static string TempPath(string name) =>
    Path.Combine(Path.GetTempPath(), $"awakening-observation-journal-{name}-{Guid.NewGuid():N}.json");

static void Cleanup(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
    try { if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); } catch { }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

readonly record struct World(
    CityRenderer City,
    InteractionDetector Detector,
    NpcCharacter Lida,
    NpcCharacter Mark,
    NpcCharacter Nika);
