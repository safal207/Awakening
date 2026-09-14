using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

const int MeetingSeed = 424251;
const int RepairSeed = 424252;
const int RefusalSeed = 424253;

string meetingPath = TempPath("meeting");
string repairPath = TempPath("repair");

try
{
    RunMeetingBranch(meetingPath);
    Console.WriteLine("FIRST_MEMORY_THREE_MORNINGS_SMOKE=PASS; branch=meeting; days=3");

    RunRepairBranch(repairPath);
    Console.WriteLine("FIRST_MEMORY_THREE_MORNINGS_SMOKE=PASS; branch=repair; days=3");

    RunRefusalBoundary();
    Console.WriteLine("FIRST_MEMORY_THREE_MORNINGS_REFUSAL=PASS; archive_without_consent=false");
}
catch (Exception e)
{
    Console.Error.WriteLine("FIRST_MEMORY_THREE_MORNINGS_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}
finally
{
    Cleanup(meetingPath);
    Cleanup(repairPath);
}

static void RunMeetingBranch(string path)
{
    HeroProgress progress = new();
    World world = BuildWorld(MeetingSeed, progress, null);

    DialogueChoice[] morning1 = world.Lida.GetDialogueState(0f, progress).choices;
    Require(HasAction(morning1, FirstMemorySlice.InspectSignalActionId), "meeting: morning 1 must offer investigation");
    Require(HasAction(morning1, FirstMemorySlice.RoutineRepairDayOneActionId), "meeting: morning 1 must offer routine repair alternative");
    Require(!HasAction(morning1, FirstMemorySlice.HelpLidaActionId), "meeting: morning 1 must not offer meeting branch");
    Require(world.Nika.NarrativeRole == NarrativeRole.Nika && world.Nika.Name == "Ника", "meeting: Nika must be third key resident");

    world.Lida.ApplyChoice(FindAction(morning1, FirstMemorySlice.InspectSignalActionId), progress);
    FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.StartDay == 1 && chapter.InvestigationCompleted && chapter.Morning(progress.Day) == 1,
        "meeting: investigation must start chapter on morning 1");
    Require(!HasAction(world.Lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId),
        "meeting: investigation must not unlock branch before next morning");

    (progress, world) = SaveReload(path, MeetingSeed, progress, world.City.Npcs);
    chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.InvestigationCompleted && chapter.StartDay == 1,
        "meeting: morning-1 chapter state must survive Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(chapter.Morning(progress.Day) == 2, "meeting: second day must be chapter morning 2");
    DialogueChoice[] morning2 = world.Lida.GetDialogueState(0f, progress).choices;
    DialogueChoice help = FindAction(morning2, FirstMemorySlice.HelpLidaActionId);
    DialogueChoice repairAlternative = FindAction(morning2, FirstMemorySlice.RepairPromiseActionId);

    world.Lida.ApplyChoice(help, progress);
    Require(chapter.Branch == FirstMemoryBranch.Meeting && chapter.BranchDay == progress.Day,
        "meeting: choosing delay must lock Meeting branch on morning 2");
    Require(FirstMemorySlice.ApplyChoice(world.Lida, repairAlternative, progress) == false,
        "meeting: repair branch must be mutually exclusive after meeting choice");
    Require(progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "meeting: branch must record concrete meeting event");

    DialogueChoice consent = FindAction(world.Mark.GetDialogueState(0f, progress).choices, FirstMemorySlice.AcceptWitnessActionId);
    world.Mark.ApplyChoice(consent, progress);
    Require(progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "meeting: Mark consent must create persisted meeting anchor");

    (progress, world) = SaveReload(path, MeetingSeed, progress, world.City.Npcs);
    chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.Branch == FirstMemoryBranch.Meeting && progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "meeting: branch + anchor must survive second Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(chapter.Morning(progress.Day) == 3 && chapter.IsOutcomeMorning(progress.Day),
        "meeting: third day must be outcome morning");
    string lidaMorning3 = world.Lida.GetDialogueState(0f, progress).npcLine;
    Require(lidaMorning3.Contains("Марк", StringComparison.Ordinal) &&
            lidaMorning3.Contains("помню", StringComparison.OrdinalIgnoreCase),
        "meeting: morning 3 must show Lida remembers Mark");

    var nikaDialogue = world.Nika.GetDialogueState(0f, progress);
    DialogueChoice archive = FindAction(nikaDialogue.choices, FirstMemorySlice.NikaArchiveActionId);
    world.Nika.ApplyChoice(archive, progress);
    Require(chapter.NikaArchived, "meeting: Nika must archive only after existing Mark consent");

    (progress, world) = SaveReload(path, MeetingSeed, progress, world.City.Npcs);
    Require(FirstMemoryChapterState.For(progress).NikaArchived,
        "meeting: Nika archive decision must survive Save/Load");
}

static void RunRepairBranch(string path)
{
    HeroProgress progress = new();
    World world = BuildWorld(RepairSeed, progress, null);

    DialogueChoice[] morning1 = world.Lida.GetDialogueState(0f, progress).choices;
    world.Lida.ApplyChoice(FindAction(morning1, FirstMemorySlice.RoutineRepairDayOneActionId), progress);
    FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.RoutineRepairDayOne && !chapter.InvestigationCompleted,
        "repair: routine repair must be a distinct morning-1 approach");

    (progress, world) = SaveReload(path, RepairSeed, progress, world.City.Npcs);
    chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.RoutineRepairDayOne && chapter.StartDay == 1,
        "repair: morning-1 routine approach must survive Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    DialogueChoice[] beforeInvestigation = world.Lida.GetDialogueState(0f, progress).choices;
    Require(HasAction(beforeInvestigation, FirstMemorySlice.InspectSignalActionId),
        "repair: repeated failure on morning 2 must unlock investigation");
    world.Lida.ApplyChoice(FindAction(beforeInvestigation, FirstMemorySlice.InspectSignalActionId), progress);

    DialogueChoice[] branchChoices = world.Lida.GetDialogueState(0f, progress).choices;
    DialogueChoice staleMeeting = FindAction(branchChoices, FirstMemorySlice.HelpLidaActionId);
    DialogueChoice repair = FindAction(branchChoices, FirstMemorySlice.RepairPromiseActionId);
    world.Lida.ApplyChoice(repair, progress);

    Require(chapter.Branch == FirstMemoryBranch.Repair,
        "repair: promised repair must lock Repair branch");
    Require(progress.Ledger.IsPersisted(FirstMemorySlice.RepairEventId),
        "repair: Lida witness + maintenance log must persist repair event");
    Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "repair: meeting event must not be created on repair branch");
    Require(FirstMemorySlice.ApplyChoice(world.Lida, staleMeeting, progress) == false,
        "repair: meeting branch must be mutually exclusive after repair choice");

    (progress, world) = SaveReload(path, RepairSeed, progress, world.City.Npcs);
    chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.Branch == FirstMemoryBranch.Repair && progress.Ledger.IsPersisted(FirstMemorySlice.RepairEventId),
        "repair: branch + repair anchor must survive Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(chapter.Morning(progress.Day) == 3 && chapter.IsOutcomeMorning(progress.Day),
        "repair: third day must be outcome morning");
    string lidaMorning3 = world.Lida.GetDialogueState(0f, progress).npcLine;
    Require(lidaMorning3.Contains("ремонт", StringComparison.OrdinalIgnoreCase) &&
            !lidaMorning3.Contains("помню нашу", StringComparison.OrdinalIgnoreCase),
        "repair: morning 3 must acknowledge kept promise without inventing meeting memory");

    var nikaDialogue = world.Nika.GetDialogueState(0f, progress);
    Require(!HasAction(nikaDialogue.choices, FirstMemorySlice.NikaArchiveActionId),
        "repair: Nika must not offer personal-meeting archive action when no meeting occurred");
}

static void RunRefusalBoundary()
{
    HeroProgress progress = new();
    World world = BuildWorld(RefusalSeed, progress, null);
    world.Lida.ApplyChoice(
        FindAction(world.Lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.InspectSignalActionId),
        progress);
    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    world.Lida.ApplyChoice(
        FindAction(world.Lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId),
        progress);

    // Mark does not accept witness consent. The branch must remain playable, but
    // the unanchored meeting is forgotten at Sverka.
    Require(!progress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "refusal: meeting must remain unanchored without Mark consent");
    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "refusal: unconsented meeting must be forgotten by Sverka");

    string lida = world.Lida.GetDialogueState(0f, progress).npcLine;
    Require(lida.Contains("имя", StringComparison.OrdinalIgnoreCase) ||
            lida.Contains("недостаточно", StringComparison.OrdinalIgnoreCase),
        "refusal: morning 3 must remain a valid consequence, not a blocked chapter");
    DialogueChoice[] nika = world.Nika.GetDialogueState(0f, progress).choices;
    Require(!HasAction(nika, FirstMemorySlice.NikaArchiveActionId),
        "refusal: Nika must not archive meeting without participant consent");
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
    Require(!loaded.progress.SaveWritesBlocked, "chapter Save/Load must remain clean");
    return (loaded.progress, BuildWorld(seed, loaded.progress, loaded.npcs));
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

static string TempPath(string branch) =>
    Path.Combine(Path.GetTempPath(), $"awakening-three-mornings-{branch}-{Guid.NewGuid():N}.json");

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
