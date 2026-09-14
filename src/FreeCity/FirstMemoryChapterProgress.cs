using System;

namespace Probuzhdenie.FreeCity;

public enum FirstMemoryBranch
{
    None,
    Meeting,
    Repair,
}

/// <summary>
/// Narrow persistent state for the first three-morning chapter. This is not a
/// generic quest engine: it only remembers the decisions that must survive a
/// Sverka so First Memory can unfold across multiple mornings.
/// </summary>
public sealed class FirstMemoryChapterProgress
{
    public int StartDay { get; private set; }
    public bool InvestigationCompleted { get; private set; }
    public bool RoutineRepairDayOne { get; private set; }
    public FirstMemoryBranch Branch { get; private set; }
    public int BranchDay { get; private set; }
    public bool NikaArchived { get; private set; }

    public bool Started => StartDay > 0;

    public int Morning(int currentDay)
    {
        if (!Started) return 1;
        return Math.Max(1, currentDay - StartDay + 1);
    }

    public void StartIfNeeded(int day)
    {
        if (StartDay <= 0)
            StartDay = Math.Max(1, day);
    }

    public bool MarkInvestigation(int day)
    {
        StartIfNeeded(day);
        if (InvestigationCompleted) return false;
        InvestigationCompleted = true;
        return true;
    }

    public bool MarkRoutineRepair(int day)
    {
        StartIfNeeded(day);
        if (Morning(day) != 1 || InvestigationCompleted || RoutineRepairDayOne) return false;
        RoutineRepairDayOne = true;
        return true;
    }

    public bool TryChooseBranch(FirstMemoryBranch branch, int day)
    {
        if (branch == FirstMemoryBranch.None) return false;
        StartIfNeeded(day);
        if (!InvestigationCompleted || Morning(day) < 2 || Branch != FirstMemoryBranch.None)
            return false;

        Branch = branch;
        BranchDay = Math.Max(1, day);
        return true;
    }

    public bool IsOutcomeMorning(int day) =>
        Branch != FirstMemoryBranch.None && BranchDay > 0 && day > BranchDay;

    public bool MarkNikaArchived()
    {
        if (NikaArchived) return false;
        NikaArchived = true;
        return true;
    }

    public void Restore(
        int startDay,
        bool investigationCompleted,
        bool routineRepairDayOne,
        FirstMemoryBranch branch,
        int branchDay,
        bool nikaArchived)
    {
        StartDay = Math.Max(0, startDay);
        InvestigationCompleted = investigationCompleted;
        RoutineRepairDayOne = routineRepairDayOne;
        Branch = Enum.IsDefined(branch) ? branch : FirstMemoryBranch.None;
        BranchDay = Math.Max(0, branchDay);
        NikaArchived = nikaArchived;

        if (StartDay == 0)
        {
            InvestigationCompleted = false;
            RoutineRepairDayOne = false;
            Branch = FirstMemoryBranch.None;
            BranchDay = 0;
            NikaArchived = false;
            return;
        }

        if (Branch == FirstMemoryBranch.None)
            BranchDay = 0;
        else if (BranchDay < StartDay)
        {
            Branch = FirstMemoryBranch.None;
            BranchDay = 0;
            NikaArchived = false;
        }
    }
}
