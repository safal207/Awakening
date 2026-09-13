using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static class FirstMemorySliceSelfTest
{
    public static bool Run(out string message)
    {
        try
        {
            CheckUnanchoredEventIsForgotten();
            CheckAnchoredEventChangesNextMorning();
            message = "First memory slice self-test passed.";
            return true;
        }
        catch (Exception e)
        {
            message = "First memory slice self-test failed: " + e.Message;
            return false;
        }
    }

    private static void CheckUnanchoredEventIsForgotten()
    {
        FirstMemorySlice.ResetRegistryForTests();
        var progress = new HeroProgress();
        var lida = MakeNpc(5101, NarrativeRole.Lida, Vector3.Zero);
        _ = MakeNpc(5102, NarrativeRole.Mark, new Vector3(20f, 0f, 0f));

        lida.ApplyChoice(FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId), progress);
        MemoryLedger ledger = FirstMemorySlice.LedgerFor(progress);
        Require(ledger.TryGetEvent(FirstMemorySlice.EventId, out _), "event should exist before Sverka");
        Require(!ledger.IsPersisted(FirstMemorySlice.EventId), "event without witness must not persist");

        progress.NewDay();
        ledger = FirstMemorySlice.LedgerFor(progress);
        Require(!ledger.TryGetEvent(FirstMemorySlice.EventId, out _), "unanchored event must be forgotten next day");
    }

    private static void CheckAnchoredEventChangesNextMorning()
    {
        FirstMemorySlice.ResetRegistryForTests();
        var progress = new HeroProgress();
        var lida = MakeNpc(5201, NarrativeRole.Lida, Vector3.Zero);
        var mark = MakeNpc(5202, NarrativeRole.Mark, new Vector3(20f, 0f, 0f));

        lida.ApplyChoice(FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId), progress);
        Require(Vector3.Distance(lida.Position, mark.Position) < 3f, "Mark should enter the meeting space");

        mark.ApplyChoice(FindAction(mark.GetDialogueState(0f, progress).choices, FirstMemorySlice.AcceptWitnessActionId), progress);
        MemoryLedger ledger = FirstMemorySlice.LedgerFor(progress);
        Require(ledger.IsPersisted(FirstMemorySlice.EventId), "accepted witness must create anchor");
        Require(ledger.TryGetAnchor(FirstMemorySlice.EventId, out var anchor) && anchor?.TraceId == FirstMemorySlice.TraceId,
            "anchor must keep dispatcher trace");

        progress.NewDay();
        string morning = lida.GetDialogueState(0f, progress).npcLine;
        Require(morning.Contains("Марк", StringComparison.Ordinal) && morning.Contains("помню", StringComparison.OrdinalIgnoreCase),
            "Lida must explicitly remember Mark next morning");
    }

    private static NpcCharacter MakeNpc(int seed, NarrativeRole role, Vector3 position)
    {
        var npc = new NpcCharacter(position, position, seed);
        npc.Position = position;
        npc.AssignNarrativeRole(role);
        return npc;
    }

    private static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
    {
        foreach (var choice in choices)
            if (choice.ActionId == actionId) return choice;
        throw new InvalidOperationException("Missing action: " + actionId);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
