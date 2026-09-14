using System;
using OpenTK.Mathematics;
using Probuzhdenie.Player;

namespace Probuzhdenie.FreeCity;

public static class FirstMemorySpatialSelfTest
{
    public static bool Run(out string message)
    {
        try
        {
            CheckSignalGateAndCityRoles();
            CheckUnanchoredSpatialReset();
            message = "First memory spatial self-test passed (2 scenarios).";
            return true;
        }
        catch (Exception e)
        {
            message = "First memory spatial self-test failed: " + e.Message;
            return false;
        }
    }

    private static void CheckSignalGateAndCityRoles()
    {
        FirstMemorySlice.ResetRegistryForTests();

        // Deliberately advance the process-global NPC ids. The named roles still
        // have to bind to this city's slots, not to absolute ids 1 and 2.
        for (int i = 0; i < 5; i++)
            _ = new NpcCharacter(Vector3.Zero, Vector3.Zero, 6100 + i);

        var progress = new HeroProgress();
        var city = new CityRenderer(424242, progress);
        var detector = new InteractionDetector(city);
        NpcCharacter lida = city.Npcs[1];
        NpcCharacter mark = city.Npcs[2];

        Require(lida.NarrativeRole == NarrativeRole.Lida && lida.Name == "Лида",
            "city-local second citizen must be Lida regardless of global NPC id");
        Require(mark.NarrativeRole == NarrativeRole.Mark && mark.Name == "Марк",
            "city-local third citizen must be Mark regardless of global NPC id");
        Require(Vector3.Distance(lida.Position, FirstMemorySpatial.SignalPosition) < 3f,
            "Lida must be staged beside the broken signal");
        Require(Vector3.Distance(mark.Position, lida.Position) > 5f,
            "Mark must start outside the talk radius");

        InteractionResult signal = detector.Detect(FirstMemorySpatial.SignalPosition);
        Require(signal.Type == InteractionType.Talk && signal.TargetNpc == lida &&
                signal.Prompt.Contains("сигнал", StringComparison.OrdinalIgnoreCase),
            "E at the prop must route to signal inspection with Lida");

        DialogueChoice[] first = lida.GetDialogueState(0f, progress).choices;
        Require(HasAction(first, FirstMemorySlice.InspectSignalActionId),
            "inspection action must exist before the causal choice");
        Require(!HasAction(first, FirstMemorySlice.HelpLidaActionId),
            "help choice must stay locked before inspection");

        lida.ApplyChoice(FindAction(first, FirstMemorySlice.InspectSignalActionId), progress);
        Require(FirstMemorySpatial.IsSignalObserved(progress), "inspection must bind to current day");
        Require(FirstMemorySpatial.SignalInteractionTarget(city, FirstMemorySpatial.SignalPosition, 3f) == null,
            "inspected signal must stop intercepting E");

        DialogueChoice[] after = lida.GetDialogueState(0f, progress).choices;
        Require(HasAction(after, FirstMemorySlice.HelpLidaActionId),
            "help choice must unlock only after signal inspection");
    }

    private static void CheckUnanchoredSpatialReset()
    {
        FirstMemorySlice.ResetRegistryForTests();
        var progress = new HeroProgress();
        var city = new CityRenderer(424243, progress);
        FirstMemorySpatial.ConfigureCity(city);
        NpcCharacter lida = city.Npcs[1];
        NpcCharacter mark = city.Npcs[2];
        Vector3 baseline = mark.Position;

        lida.ApplyChoice(FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.InspectSignalActionId), progress);
        lida.ApplyChoice(FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId), progress);
        Require(progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
            "spatial choice must create the concrete event");
        Require(Vector3.Distance(mark.Position, lida.Position) < 3f,
            "helping Lida must physically bring Mark into the meeting space");

        progress.NewDay();
        FirstMemorySpatial.SyncDay(progress);
        Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
            "unanchored event must be forgotten by Sverka");
        Require(Vector3.Distance(mark.Position, baseline) < 0.01f,
            "unanchored spatial consequence must reset to Mark's baseline");
        Require(!FirstMemorySpatial.IsSignalObserved(progress),
            "signal inspection must reset on a new day when no anchor survived");
    }

    private static bool HasAction(DialogueChoice[] choices, string actionId)
    {
        foreach (var choice in choices)
            if (choice.ActionId == actionId) return true;
        return false;
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
