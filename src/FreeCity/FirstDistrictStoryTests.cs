using System.Linq;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class FirstDistrictStoryTests
{
    public static bool Run(out string message)
    {
        bool persistedPath = RunPersistedPath(out string persistedDetail);
        bool refusedPath = RunRefusedPath(out string refusedDetail);
        MemoryRuntime.Reset();

        bool ok = persistedPath && refusedPath;
        message = ok
            ? "First district story tests passed."
            : $"First district story tests failed: persisted={persistedPath} ({persistedDetail}), refused={refusedPath} ({refusedDetail}).";
        return ok;
    }

    private static bool RunPersistedPath(out string detail)
    {
        MemoryRuntime.Reset();
        MemoryRuntime.HeroId = 0;
        var progress = new HeroProgress();
        var city = new CityRenderer(424242, progress);
        var lida = city.Npcs[1];
        var mark = city.Npcs[2];

        if (!FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var lidaChoices) || lidaChoices.Length < 1)
        {
            detail = "missing Lida day-1 choice";
            return false;
        }
        lida.ApplyChoice(lidaChoices[0], progress);
        ArrangeMeeting(city);

        var candidate = MemoryRuntime.Current.FindAnchor(FirstDistrictStory.MeetingEventId);
        if (candidate == null || candidate.Status != MemoryAnchorStatus.Candidate)
        {
            detail = "meeting candidate was not created";
            return false;
        }

        if (!FirstDistrictStory.TryGetDialogue(mark, progress, out _, out var markChoices) || markChoices.Length < 1)
        {
            detail = "missing Mark witness choice";
            return false;
        }
        mark.ApplyChoice(markChoices[0], progress);

        var witness = candidate.Witnesses.SingleOrDefault(w => w.NpcId == mark.Id);
        if (witness == null || !witness.UnderstoodEvent || !witness.Consented)
        {
            detail = "Mark did not become an informed voluntary witness";
            return false;
        }

        progress.NewDay();
        bool lidaMorning = FirstDistrictStory.TryGetDialogue(lida, progress, out string lidaLine, out _)
            && lidaLine.Contains("помню его имя");
        bool markMorning = FirstDistrictStory.TryGetDialogue(mark, progress, out string markLine, out _)
            && markLine.Contains("точно помню");

        bool ok = progress.Day == 2 &&
                  MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId) &&
                  MemoryRuntime.LastSverkaReport?.PersistedCount == 1 &&
                  lidaMorning && markMorning;
        detail = ok ? "ok" : $"day={progress.Day}, persisted={MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId)}, lidaMorning={lidaMorning}, markMorning={markMorning}";
        return ok;
    }

    private static bool RunRefusedPath(out string detail)
    {
        MemoryRuntime.Reset();
        MemoryRuntime.HeroId = 0;
        var progress = new HeroProgress();
        var city = new CityRenderer(424242, progress);
        var lida = city.Npcs[1];
        var mark = city.Npcs[2];

        FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var lidaChoices);
        lida.ApplyChoice(lidaChoices[0], progress);
        ArrangeMeeting(city);
        mark.Trust = 0f;
        FirstDistrictStory.TryGetDialogue(mark, progress, out _, out var markChoices);
        mark.ApplyChoice(markChoices[0], progress);

        var anchor = MemoryRuntime.Current.FindAnchor(FirstDistrictStory.MeetingEventId);
        var witness = anchor?.Witnesses.SingleOrDefault(w => w.NpcId == mark.Id);
        bool informedRefusal = witness != null && witness.UnderstoodEvent && !witness.Consented;

        progress.NewDay();
        bool lidaMorning = FirstDistrictStory.TryGetDialogue(lida, progress, out string lidaLine, out _)
            && lidaLine.Contains("Не припоминаю");
        bool markMorning = FirstDistrictStory.TryGetDialogue(mark, progress, out string markLine, out _)
            && markLine.Contains("дальше — пусто");

        bool ok = informedRefusal &&
                  anchor?.Status == MemoryAnchorStatus.Rejected &&
                  !MemoryRuntime.Current.HasPersisted(FirstDistrictStory.MeetingEventId) &&
                  MemoryRuntime.LastSverkaReport?.RejectedCount == 1 &&
                  lidaMorning && markMorning;
        detail = ok ? "ok" : $"informedRefusal={informedRefusal}, status={anchor?.Status}, lidaMorning={lidaMorning}, markMorning={markMorning}";
        return ok;
    }

    internal static void ArrangeMeeting(CityRenderer city)
    {
        var episode = city.Progress.DistrictEpisode;
        if (episode.Phase == DistrictPhase.Routine) episode.Plan(true, city.Progress.Day);
        city.Player!.Position = FirstDistrictEpisode.Signal;
        city.InteractWithDistrict(DistrictInteraction.Signal);
        city.Npcs[2].Trust = 10f;
        city.Npcs[2].Friendliness = 20f;
        FirstDistrictStory.TryGetDialogue(city.Npcs[2], city.Progress, out _, out var choices);
        city.Npcs[2].ApplyChoice(choices[0], city.Progress);
        city.Player.Position = FirstDistrictEpisode.Stop + new Vector3(0,0,-2);
        for (int i = 0; i < 180; i++)
        {
            city.AdvanceDayClock(0.1f);
            city.UpdateDistrict(0.1f);
        }
    }
}
