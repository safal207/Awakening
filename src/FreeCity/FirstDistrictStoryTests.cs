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
        var lida = StoryNpc(1, FirstDistrictStory.LidaName);
        var mark = StoryNpc(2, FirstDistrictStory.MarkName);

        if (!FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var lidaChoices) || lidaChoices.Length < 1)
        {
            detail = "missing Lida day-1 choice";
            return false;
        }
        lida.ApplyChoice(lidaChoices[0], progress);

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
        var lida = StoryNpc(1, FirstDistrictStory.LidaName);
        var mark = StoryNpc(2, FirstDistrictStory.MarkName);
        mark.Trust = 0f;

        FirstDistrictStory.TryGetDialogue(lida, progress, out _, out var lidaChoices);
        lida.ApplyChoice(lidaChoices[0], progress);
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

    private static NpcCharacter StoryNpc(int id, string name)
    {
        var npc = new NpcCharacter(Vector3.Zero, Vector3.Zero, 9000 + id)
        {
            Id = id,
            Name = name,
        };
        return npc;
    }
}
