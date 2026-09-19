using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

const int Seed = 425101;
string path = Path.Combine(Path.GetTempPath(), $"awakening-findings-{Guid.NewGuid():N}.json");

try
{
    HeroProgress progress = new();
    World world = BuildWorld(Seed, progress, null);

    // Engage with the signal first so morning-one findings become actionable.
    DialogueChoice inspect = FindAction(
        world.Lida.GetDialogueState(0f, progress).choices,
        FirstMemorySlice.InspectSignalActionId);
    world.Lida.ApplyChoice(inspect, progress);

    float memory0 = progress.Memory;
    float curiosity0 = progress.Curiosity;
    float empathy0 = progress.Empathy;
    float agency0 = progress.Agency;
    float courage0 = progress.Courage;

    DiscoverCurrentMorning(progress, world, expectedNew: 2, expectedMorning: 1);
    Require(CountFindingEntries(progress) == 2, "morning 1 must add exactly two findings");

    (progress, world) = SaveReload(path, progress, world.City.Npcs);
    Require(CountFindingEntries(progress) == 2, "morning-1 findings must survive Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    DiscoverCurrentMorning(progress, world, expectedNew: 2, expectedMorning: 2);
    Require(CountFindingEntries(progress) == 4, "morning 2 must bring total to four findings");

    (progress, world) = SaveReload(path, progress, world.City.Npcs);
    Require(CountFindingEntries(progress) == 4, "morning-2 findings must survive Save/Load");

    progress.NewDay();
    _ = world.Detector.Detect(FirstMemorySpatial.SignalPosition);
    DiscoverCurrentMorning(progress, world, expectedNew: 2, expectedMorning: 3);

    Require(CountFindingEntries(progress) == 6, "all six finding IDs must exist exactly once");
    RequireUniqueFindingIds(progress);
    Require(FirstMemoryFindings.All.Count == 6, "catalog must contain exactly six bounded findings");

    float qualityGain =
        MathF.Abs(progress.Memory - memory0) +
        MathF.Abs(progress.Curiosity - curiosity0) +
        MathF.Abs(progress.Empathy - empathy0) +
        MathF.Abs(progress.Agency - agency0) +
        MathF.Abs(progress.Courage - courage0);
    Require(qualityGain < 0.001f, $"findings must not grant qualities; gain={qualityGain}");
    Require(MathF.Abs(world.City.Awareness.Level) < 0.001f,
        "findings must not grant world Awareness");

    Console.WriteLine(
        "STORY_FINDINGS_SMOKE=PASS; findings=6; duplicates=0; quality_gain=0; awareness_gain=0");
}
catch (Exception e)
{
    Console.Error.WriteLine("STORY_FINDINGS_SMOKE=FAIL; " + e.Message);
    Environment.ExitCode = 1;
}
finally
{
    try { if (File.Exists(path)) File.Delete(path); } catch { }
    try { if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); } catch { }
}

static void DiscoverCurrentMorning(
    HeroProgress progress,
    World world,
    int expectedNew,
    int expectedMorning)
{
    FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
    Require(chapter.Morning(progress.Day) == expectedMorning,
        $"expected chapter morning {expectedMorning}, got {chapter.Morning(progress.Day)}");

    var available = new List<StoryFinding>();
    foreach (StoryFinding finding in FirstMemoryFindings.All)
    {
        Require(world.City.IsPositionWalkable(finding.Position, 0.20f),
            $"finding {finding.Id} must be placed on walkable exterior geometry");

        if (FirstMemoryFindings.IsAvailable(progress, finding))
            available.Add(finding);
    }

    Require(available.Count == expectedNew,
        $"morning {expectedMorning}: expected {expectedNew} newly available findings, got {available.Count}");

    foreach (StoryFinding finding in available)
    {
        InteractionResult interaction = world.Detector.Detect(finding.Position);
        Require(interaction.Type == InteractionType.Finding,
            $"finding {finding.Id} must be an explicit E interaction");
        Require(string.Equals(interaction.FindingId, finding.Id, StringComparison.Ordinal),
            $"interaction must preserve stable finding id {finding.Id}");

        Require(FirstMemoryFindings.TryDiscover(progress, interaction.FindingId!, out StoryFinding? discovered) &&
                discovered?.Id == finding.Id,
            $"finding {finding.Id} must be discoverable once");

        Require(!FirstMemoryFindings.TryDiscover(progress, finding.Id, out _),
            $"finding {finding.Id} must reject duplicate discovery");
    }
}

static int CountFindingEntries(HeroProgress progress)
{
    int count = 0;
    foreach (ObservationEntry entry in ObservationJournalState.For(progress).Entries)
        if (entry.Id.StartsWith(FirstMemoryFindings.JournalPrefix, StringComparison.Ordinal))
            count++;
    return count;
}

static void RequireUniqueFindingIds(HeroProgress progress)
{
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (ObservationEntry entry in ObservationJournalState.For(progress).Entries)
    {
        if (!entry.Id.StartsWith(FirstMemoryFindings.JournalPrefix, StringComparison.Ordinal))
            continue;
        Require(ids.Add(entry.Id), "duplicate finding journal id: " + entry.Id);
    }
    Require(ids.Count == 6, "finding journal must contain six unique stable IDs");
}

static World BuildWorld(int seed, HeroProgress progress, List<SaveSystem.NpcSaveData>? rows)
{
    var city = new CityRenderer(seed, progress);
    city.RestoreNpcs(rows);
    var detector = new InteractionDetector(city);
    return new World(city, detector, city.Npcs[1]);
}

static (HeroProgress progress, World world) SaveReload(
    string path,
    HeroProgress progress,
    IReadOnlyList<NpcCharacter> npcs)
{
    MethodInfo save = typeof(SaveSystem).GetMethod("SaveToPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SaveToPath reflection target missing");
    save.Invoke(null, new object?[] { path, Seed, progress, new AwarenessSystem(), 10f, DateTime.UtcNow, npcs });

    MethodInfo load = typeof(SaveSystem).GetMethod("LoadFromPath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("LoadFromPath reflection target missing");
    object raw = load.Invoke(null, new object?[] { path })
        ?? throw new InvalidOperationException("LoadFromPath returned null");
    var loaded = ((int seed, HeroProgress progress, float timeOfDay, float awareness, double offlineMinutes, List<SaveSystem.NpcSaveData>? npcs))raw;
    Require(!loaded.progress.SaveWritesBlocked, "finding Save/Load must remain clean");
    return (loaded.progress, BuildWorld(Seed, loaded.progress, loaded.npcs));
}

static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
{
    foreach (DialogueChoice choice in choices)
        if (choice.ActionId == actionId) return choice;
    throw new InvalidOperationException("Missing action: " + actionId);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

readonly record struct World(
    CityRenderer City,
    InteractionDetector Detector,
    NpcCharacter Lida);
