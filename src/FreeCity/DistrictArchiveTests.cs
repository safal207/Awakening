using System;
using System.IO;
using OpenTK.Mathematics;
using Probuzhdenie.Player;

namespace Probuzhdenie.FreeCity;

public static class DistrictArchiveTests
{
    public static bool Run(out string message)
    {
        var previous = MemoryRuntime.Current;
        int hero = MemoryRuntime.HeroId;
        string save = SaveSystem.SaveFilePath;
        SaveSystem.SaveFilePath = Path.GetFullPath(Path.Combine("artifacts", "archive-tests", Guid.NewGuid().ToString("N"), "save.json"));
        try
        {
            var city = Morning(meeting: true);
            Check(new InteractionDetector(city).Detect(DistrictArchiveStory.NikaPosition + new Vector3(0, 0, -1.5f)).TargetNpc?.Id == 3,
                "Nika is reachable without journal stealing interaction");
            Vector3 nika = city.Npcs[3].Position;
            for (int i = 0; i < 100; i++) { city.UpdateCitizens(.03f); city.UpdateDistrict(.03f); }
            Check(city.Npcs[3].Position == nika, "Nika stays at archive and is not pushed by crowd");
            var ask = Choose(city, 3);
            Check(!city.Npcs[1].ApplyChoice(ask, city.Progress), "wrong actor cannot request archive");
            var lidaPermission = Choose(city, 1);
            Check(DistrictArchiveStory.Permission(1) == true && DistrictArchiveStory.Permission(2) == null, "individual permissions");
            city = Reload(city);
            Check(DistrictArchiveStory.Requested && DistrictArchiveStory.Permission(1) == true, "request and partial consent survive file load");
            Check(!city.Npcs[1].ApplyChoice(lidaPermission, city.Progress), "permission cannot be replayed after load");
            Choose(city, 3); // Not enough signatures: response only.
            Check(DistrictArchiveStory.Outcome == ArchiveOutcome.None, "one permission cannot publish");
            Choose(city, 2);
            var publish = Choose(city, 3);
            Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Meeting && !MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId), "entry waits for Sverka");
            Check(!city.Npcs[3].ApplyChoice(publish, city.Progress), "publication is one-shot");
            city = Reload(city);
            Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Meeting && MemoryRuntime.Current.FindAnchor(DistrictArchiveStory.EntryId)?.Status == MemoryAnchorStatus.Candidate,
                "pending page survives load");
            MorningAtBench(city);
            Check(city.Progress.Day == 3 && MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId), "third morning retains page");
            Check(Line(city, 1).Contains("Доброе утро, Марк") && Line(city, 2).Contains("продолжение"), "both participants react to third morning");
            city = Reload(city);
            int events = MemoryRuntime.Current.Events.Count;
            float memory = city.Progress.Memory;
            for (int i = 0; i < 10; i++) MorningAtBench(city);
            Check(events == MemoryRuntime.Current.Events.Count && memory == city.Progress.Memory, "later mornings do not clone entries or reward waiting");
            Check(!city.Npcs[3].ApplyChoice(publish, city.Progress), "stale choice stays unavailable");

            foreach (int refusing in new[] { 1, 2 })
            {
                city = Morning(meeting: true);
                Choose(city, 3);
                city.Npcs[refusing].Trust = 0;
                var refusal = Choose(city, refusing);
                Check(DistrictArchiveStory.Permission(refusing) == false, "participant independently refuses publication");
                city.Npcs[refusing].Trust = 100;
                Check(!city.Npcs[refusing].ApplyChoice(refusal, city.Progress), "more trust cannot overwrite recorded refusal");
                Choose(city, 3);
                city = Reload(city);
                MorningAtBench(city);
                Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Refused && MemoryRuntime.Current.FindAnchor(DistrictArchiveStory.EntryId) == null,
                    "refusal never fabricates a public memory anchor");
                Check(MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId) && Line(city, 1).Contains("помню"),
                    "publication refusal does not erase shared memory");
            }
            city = Morning(meeting: true);
            Choose(city, 3, 1);
            MorningAtBench(city);
            Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Private && DistrictArchiveStory.Permission(1) == null, "private branch does not invent permissions");

            city = Morning(meeting: false, repair: true);
            Choose(city, 3);
            city = Reload(city);
            MorningAtBench(city);
            Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Repair && MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId), "repair has its own lasting evidence");
            Check(MemoryRuntime.Current.FindEvent(FirstDistrictStory.MeetingEventId) == null && Line(city, 1).Contains("выполнил обещание"), "repair does not invent meeting");

            foreach (bool rejectedMeeting in new[] { false, true })
            {
                city = Morning(meeting: rejectedMeeting, witness: false);
                Choose(city, 3);
                MorningAtBench(city);
                Check(DistrictArchiveStory.Outcome == ArchiveOutcome.Unconfirmed && !MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId),
                    "missed or rejected meeting cannot be laundered through archive");
            }
            city = Morning(meeting: true);
            Choose(city, 3);
            Choose(city, 1);
            MorningAtBench(city);
            city = Reload(city);
            Choose(city, 2);
            Choose(city, 3);
            Check(city.Progress.Day == 3 && !MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId), "late completion still waits for its own morning");
            MorningAtBench(city);
            Check(MemoryRuntime.Current.HasPersisted(DistrictArchiveStory.EntryId), "skipping morning does not block continuation");
            message = "Archive story tests passed: permissions, refusal, private/repair/unconfirmed branches, third morning, delayed completion and file round trips.";
            return true;
        }
        catch (Exception e) { message = "Archive story tests failed: " + e; return false; }
        finally { SaveSystem.SaveFilePath = save; MemoryRuntime.Replace(previous); MemoryRuntime.HeroId = hero; }
    }

    private static CityRenderer Morning(bool meeting, bool repair = false, bool witness = true)
    {
        MemoryRuntime.Reset();
        var city = new CityRenderer(424242);
        city.Npcs[1].Trust = 10;
        city.Npcs[1].Friendliness = 20;
        var early = new DialogueChoice("test", 0, 0, 0, 0, 0, 0, 0, ActionId: "district1.archive.ask");
        Check(!city.Npcs[3].ApplyChoice(early, city.Progress), "archive locked before second morning");
        if (meeting)
        {
            FirstDistrictStoryTests.ArrangeMeeting(city);
            Choose(city, 2, witness ? 0 : 1);
        }
        else if (repair)
        {
            city.Progress.DistrictEpisode.Plan(false, 1);
            city.Progress.DistrictEpisode.ActivateSignal(FirstDistrictEpisode.Signal, 8, 1);
        }
        else city.Progress.DistrictEpisode.EndDay();
        MorningAtBench(city);
        return city;
    }

    private static void MorningAtBench(CityRenderer city)
    {
        city.Player!.Position = FirstDistrictEpisode.Rest;
        Check(city.InteractWithDistrict(DistrictInteraction.Rest), "bench allows next morning");
    }

    private static DialogueChoice Choose(CityRenderer city, int npcId, int index = 0)
    {
        FirstDistrictStory.TryGetDialogue(city.Npcs[npcId], city.Progress, out _, out var choices);
        Check(index < choices.Length && city.Npcs[npcId].ApplyChoice(choices[index], city.Progress), "choice applies for NPC " + npcId);
        return choices[index];
    }

    private static string Line(CityRenderer city, int npcId)
    {
        FirstDistrictStory.TryGetDialogue(city.Npcs[npcId], city.Progress, out string line, out _);
        return line;
    }

    private static CityRenderer Reload(CityRenderer city)
    {
        Check(SaveSystem.Save(424242, city.Progress, new AwarenessSystem(), city.TimeOfDay, city.Npcs, MemoryRuntime.Current), "save archive");
        var loaded = SaveSystem.Load();
        MemoryRuntime.Replace(loaded.memoryLedger);
        var restored = new CityRenderer(loaded.seed, loaded.progress) { TimeOfDay = loaded.timeOfDay };
        restored.RestoreNpcs(loaded.npcs);
        return restored;
    }

    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
