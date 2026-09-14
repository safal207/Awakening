using System;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static bool HasAction(DialogueChoice[] choices, string actionId)
{
    foreach (var choice in choices)
        if (choice.ActionId == actionId) return true;
    return false;
}

static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
{
    foreach (var choice in choices)
        if (choice.ActionId == actionId) return choice;
    throw new InvalidOperationException("Missing action: " + actionId);
}

try
{
    // Shift process-global NPC ids first. Named roles must still be bound to this city.
    for (int i = 0; i < 5; i++)
        _ = new NpcCharacter(Vector3.Zero, Vector3.Zero, 7100 + i);

    var progress = new HeroProgress();
    var city = new CityRenderer(424242, progress);
    var detector = new InteractionDetector(city);
    NpcCharacter lida = city.Npcs[1];
    NpcCharacter mark = city.Npcs[2];
    Vector3 baseline = mark.Position;

    Require(lida.NarrativeRole == NarrativeRole.Lida && lida.Name == "Лида",
        "city-local second citizen must be Lida");
    Require(mark.NarrativeRole == NarrativeRole.Mark && mark.Name == "Марк",
        "city-local third citizen must be Mark");
    Require(Vector3.Distance(lida.Position, FirstMemorySpatial.SignalPosition) < 3f,
        "Lida must stand beside the broken signal");
    Require(Vector3.Distance(mark.Position, lida.Position) > 5f,
        "Mark must start outside talk radius");

    InteractionResult signal = detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(signal.Type == InteractionType.Talk && signal.TargetNpc == lida &&
            signal.Prompt.Contains("сигнал", StringComparison.OrdinalIgnoreCase),
        "E at signal must route to the inspection interaction");

    DialogueChoice[] before = lida.GetDialogueState(0f, progress).choices;
    Require(HasAction(before, FirstMemorySlice.InspectSignalActionId),
        "inspection action must exist before help");
    Require(!HasAction(before, FirstMemorySlice.HelpLidaActionId),
        "help action must be locked before inspection");

    lida.ApplyChoice(FindAction(before, FirstMemorySlice.InspectSignalActionId), progress);
    Require(FirstMemorySpatial.IsSignalObserved(progress), "signal must become observed");

    InteractionResult afterInspect = detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(!afterInspect.Prompt.Contains("Осмотреть сигнал", StringComparison.OrdinalIgnoreCase),
        "observed signal must stop intercepting E");

    DialogueChoice help = FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId);
    lida.ApplyChoice(help, progress);
    Require(progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out var eventData) &&
            eventData?.LocationId == FirstMemorySlice.LocationId,
        "helping Lida must create the tram-plaza event");
    Require(Vector3.Distance(mark.Position, lida.Position) < 3f,
        "help choice must bring Mark into the meeting space");

    progress.NewDay();
    _ = detector.Detect(FirstMemorySpatial.SignalPosition); // sync spatial day state
    Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "unanchored event must be forgotten by Sverka");
    Require(Vector3.Distance(mark.Position, baseline) < 0.01f,
        "unanchored Mark movement must reset to baseline");
    Require(!FirstMemorySpatial.IsSignalObserved(progress),
        "signal observation must reset on the new day");

    // Separate anchored scenario: the spatial consequence survives into the next morning.
    var anchoredProgress = new HeroProgress();
    var anchoredCity = new CityRenderer(424244, anchoredProgress);
    var anchoredDetector = new InteractionDetector(anchoredCity);
    NpcCharacter anchoredLida = anchoredCity.Npcs[1];
    NpcCharacter anchoredMark = anchoredCity.Npcs[2];

    anchoredLida.ApplyChoice(
        FindAction(anchoredLida.GetDialogueState(0f, anchoredProgress).choices, FirstMemorySlice.InspectSignalActionId),
        anchoredProgress);
    anchoredLida.ApplyChoice(
        FindAction(anchoredLida.GetDialogueState(0f, anchoredProgress).choices, FirstMemorySlice.HelpLidaActionId),
        anchoredProgress);
    anchoredMark.ApplyChoice(
        FindAction(anchoredMark.GetDialogueState(0f, anchoredProgress).choices, FirstMemorySlice.AcceptWitnessActionId),
        anchoredProgress);

    Require(anchoredProgress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "accepted witness must create a persisted anchor");

    anchoredProgress.NewDay();
    _ = anchoredDetector.Detect(FirstMemorySpatial.SignalPosition);
    Require(anchoredProgress.Ledger.IsPersisted(FirstMemorySlice.EventId),
        "anchored event must survive Sverka");
    Require(Vector3.Distance(anchoredMark.Position, anchoredLida.Position) < 3f,
        "anchored relationship must remain spatially visible the next morning");

    string morning = anchoredLida.GetDialogueState(0f, anchoredProgress).npcLine;
    Require(morning.Contains("Марк", StringComparison.Ordinal) &&
            morning.Contains("помню", StringComparison.OrdinalIgnoreCase),
        "next-morning Lida dialogue must be caused by the persisted event");

    Console.WriteLine("FIRST_MEMORY_SPATIAL_SMOKE=PASS; scenarios=2");
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("FIRST_MEMORY_SPATIAL_SMOKE=FAIL: " + e.Message);
    return 1;
}
