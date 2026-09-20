using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

const int Seed = 425201;
string path = Path.Combine(Path.GetTempPath(), $"awakening-early-sleep-{Guid.NewGuid():N}.json");

try
{
    VerifySleepingBeforeChapterDoesNotBlockStory();

    HeroProgress progress = new();
    World world = BuildWorld(Seed, progress, null, 8f);

    // Morning 1: investigate and create one journal finding.
    DialogueChoice inspect = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.InspectSignalActionId);
    world.Lida.ApplyChoice(inspect, progress);
    StoryFinding firstFinding = FirstMemoryFindings.All[0];
    Require(FirstMemoryFindings.TryDiscover(progress, firstFinding.Id, out _),
        "morning 1 finding should be discoverable after investigation");
    int journalBeforeSleep = ObservationJournalState.For(progress).Entries.Count;

    progress.RegisterDailyTalk(world.Lida.Id);
    SaveToTemp(path, progress, world.City.Npcs, world.City.TimeOfDay);
    Require(progress.Day == 1, "pre-Sverka save must still be day 1");

    int firstTransition = world.City.AdvanceToNextMorning();
    Require(firstTransition == 1 && progress.Day == 2,
        "first early sleep must perform exactly one day transition");
    Require(MathF.Abs(world.City.TimeOfDay - CityRenderer.DefaultMorningHour) < 0.001f,
        "early sleep must start next day at 08:00");
    Require(progress.DailyTalkProgress == 0,
        "daily objective must reset at the day boundary");
    Require(Vector3.Distance(world.City.Player!.Position, CityRenderer.MorningSpawn) < 0.001f,
        "hero must return to stable morning spawn");
    Require(ObservationJournalState.For(progress).Entries.Count == journalBeforeSleep,
        "journal entries must survive the first Sverka");

    SaveToTemp(path, progress, world.City.Npcs, world.City.TimeOfDay);
    Require(File.Exists(path + ".bak"),
        "second save must preserve exact pre-Sverka day as verified backup");

    var backupDay1 = LoadFromTemp(path + ".bak");
    Require(backupDay1.progress.Day == 1,
        "backup after first early sleep must contain the exact previous day");

    (progress, world) = ReloadPrimary(path);
    Require(progress.Day == 2 && MathF.Abs(world.City.TimeOfDay - 8f) < 0.001f,
        "reloaded primary must open directly on morning 2");
    Require(ObservationJournalState.For(progress).Entries.Count == journalBeforeSleep,
        "journal must survive early sleep plus Save/Load");

    // Morning 2: choose Meeting and anchor it with Mark consent.
    DialogueChoice[] morning2 = world.Lida.GetDialogueState(0f, progress).choices;
    world.Lida.ApplyChoice(FindAction(morning2, FirstMemorySlice.HelpLidaActionId), progress);
    world.Mark.ApplyChoice(
        FindAction(world.Mark.GetDialogueState(0f, progress).choices, FirstMemorySlice.AcceptWitnessActionId),
        progress);
    Require(progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "meeting must be anchored before second early sleep");
    Require(progress.Ledger.Anchors.Count == 1,
        "meeting branch should contain one anchor before second transition");

    // Persistent identity/state must survive ordinary daily reset.
    world.PersistentNpc.Awaken();
    Require(world.PersistentNpc.IsAwakened, "test resident must be awakened before sleep");

    // Simulate being inside without creating GL geometry; early sleep must clear context.
    SetPrivateField(world.City, "_inside", true);
    Require(world.City.IsInside, "test setup must mark city as inside");

    SaveToTemp(path, progress, world.City.Npcs, world.City.TimeOfDay);
    int secondTransition = world.City.AdvanceToNextMorning();

    Require(secondTransition == 1 && progress.Day == 3,
        "second early sleep must also perform exactly one Sverka");
    Require(!world.City.IsInside, "early sleep must close interior context");
    Require(world.PersistentNpc.IsAwakened,
        "persistent NPC awakening must survive early sleep");
    Require(progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "anchored meeting must survive early sleep Sverka");
    Require(progress.Ledger.Anchors.Count == 1,
        "early sleep must not duplicate MemoryAnchor");
    Require(Vector3.Distance(world.City.Player!.Position, CityRenderer.MorningSpawn) < 0.001f,
        "day 3 must begin at morning spawn");

    SaveToTemp(path, progress, world.City.Npcs, world.City.TimeOfDay);
    var backupDay2 = LoadFromTemp(path + ".bak");
    Require(backupDay2.progress.Day == 2,
        "backup after second early sleep must contain day 2 pre-Sverka state");

    (progress, world) = ReloadPrimary(path);
    Require(progress.Day == 3 && MathF.Abs(world.City.TimeOfDay - 8f) < 0.001f,
        "reloaded primary must open directly on morning 3");
    Require(progress.Ledger.IsPersisted(FirstMemorySlice.EventId) &&
            progress.Ledger.Anchors.Count == 1,
        "morning-3 reload must preserve exactly one anchor");

    string lidaMorning3 = world.Lida.GetDialogueState(0f, progress).npcLine;
    Require(lidaMorning3.Contains("Марк", StringComparison.Ordinal) &&
            lidaMorning3.Contains("помню", StringComparison.OrdinalIgnoreCase),
        "early-sleep path must reach the same branch-specific morning-3 consequence");

    Console.WriteLine(
        "EARLY_SLEEP_SMOKE=PASS; transitions=2; start=1; end=3; time=8; duplicate_anchors=0");
}
catch (Exception e)
{
    Console.Error.WriteLine("EARLY_SLEEP_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}
finally
{
    Cleanup(path);
    Cleanup(path + ".bak");
}

static void VerifySleepingBeforeChapterDoesNotBlockStory()
{
    HeroProgress progress = new();
    World world = BuildWorld(425202, progress, null, 8f);

    int transition = world.City.AdvanceToNextMorning();
    Require(transition == 1 && progress.Day == 2,
        "pre-chapter early sleep should advance only one day");

    FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.StartDay == 0 && chapter.Morning(progress.Day) == 1,
        "chapter Morning 1 must remain available if player sleeps before starting it");

    DialogueChoice[] choices = world.Lida.GetDialogueState(0f, progress).choices;
    Require(HasAction(choices, FirstMemorySlice.InspectSignalActionId) &&
            HasAction(choices, FirstMemorySlice.RoutineRepairDayOneActionId),
        "sleeping before chapter start must not skip Morning-1 choices");
}

static World BuildWorld(
    int seed,
    HeroProgress progress,
    List<SaveSystem.NpcSaveData>? rows,
    float timeOfDay)
{
    var city = new CityRenderer(seed, progress) { TimeOfDay = timeOfDay };
    city.RestoreNpcs(rows);
    var detector = new InteractionDetector(city);
    return new World(city, detector, city.Npcs[1], city.Npcs[2], city.Npcs[4]);
}

static (HeroProgress progress, World world) ReloadPrimary(string path)
{
    var loaded = LoadFromTemp(path);
    Require(!loaded.progress.SaveWritesBlocked, "early-sleep primary must reload cleanly");
    return (
        loaded.progress,
        BuildWorld(loaded.seed, loaded.progress, loaded.npcs, loaded.timeOfDay));
}

static void SaveToTemp(
    string path,
    HeroProgress progress,
    IReadOnlyList<NpcCharacter> npcs,
    float timeOfDay)
{
    MethodInfo save = typeof(SaveSystem).GetMethod(
        "SaveToPath",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SaveToPath reflection target missing");

    save.Invoke(null, new object?[]
    {
        path,
        Seed,
        progress,
        new AwarenessSystem(),
        timeOfDay,
        DateTime.UtcNow,
        npcs,
    });
}

static (int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<SaveSystem.NpcSaveData>? npcs)
    LoadFromTemp(string path)
{
    MethodInfo load = typeof(SaveSystem).GetMethod(
        "LoadFromPath",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("LoadFromPath reflection target missing");

    object raw = load.Invoke(null, new object?[] { path })
        ?? throw new InvalidOperationException("LoadFromPath returned null");

    return ((int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<SaveSystem.NpcSaveData>? npcs))raw;
}

static void SetPrivateField(object target, string fieldName, object value)
{
    FieldInfo field = target.GetType().GetField(
        fieldName,
        BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Missing field: " + fieldName);
    field.SetValue(target, value);
}

static bool HasAction(DialogueChoice[] choices, string actionId)
{
    foreach (DialogueChoice choice in choices)
        if (choice.ActionId == actionId) return true;
    return false;
}

static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
{
    foreach (DialogueChoice choice in choices)
        if (choice.ActionId == actionId) return choice;
    throw new InvalidOperationException("Missing action: " + actionId);
}

static void Cleanup(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
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
    NpcCharacter PersistentNpc);
