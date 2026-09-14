using System;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    public sealed class FirstMemoryChapterSaveData
    {
        public int StartDay { get; set; }
        public bool InvestigationCompleted { get; set; }
        public bool RoutineRepairDayOne { get; set; }
        public string Branch { get; set; } = nameof(FirstMemoryBranch.None);
        public int BranchDay { get; set; }
        public bool NikaArchived { get; set; }
    }
}

public static class FirstMemoryChapterPersistence
{
    public static SaveSystem.FirstMemoryChapterSaveData Capture(HeroProgress progress)
    {
        FirstMemoryChapterProgress chapter = FirstMemoryChapterState.For(progress);
        return new SaveSystem.FirstMemoryChapterSaveData
        {
            StartDay = chapter.StartDay,
            InvestigationCompleted = chapter.InvestigationCompleted,
            RoutineRepairDayOne = chapter.RoutineRepairDayOne,
            Branch = chapter.Branch.ToString(),
            BranchDay = chapter.BranchDay,
            NikaArchived = chapter.NikaArchived,
        };
    }

    public static bool TryRestore(HeroProgress progress, SaveSystem.FirstMemoryChapterSaveData? data)
    {
        if (data == null)
            return true; // legacy save: chapter not started

        if (data.StartDay < 0 || data.BranchDay < 0)
            return false;
        if (!Enum.TryParse(data.Branch, out FirstMemoryBranch branch) || !Enum.IsDefined(branch))
            return false;
        if (data.StartDay == 0 &&
            (data.InvestigationCompleted || data.RoutineRepairDayOne ||
             branch != FirstMemoryBranch.None || data.BranchDay != 0 || data.NikaArchived))
            return false;
        if (branch == FirstMemoryBranch.None && data.BranchDay != 0)
            return false;
        if (branch != FirstMemoryBranch.None &&
            (data.StartDay == 0 || data.BranchDay < data.StartDay || !data.InvestigationCompleted))
            return false;

        FirstMemoryChapterState.For(progress).Restore(
            data.StartDay,
            data.InvestigationCompleted,
            data.RoutineRepairDayOne,
            branch,
            data.BranchDay,
            data.NikaArchived);
        return true;
    }
}
