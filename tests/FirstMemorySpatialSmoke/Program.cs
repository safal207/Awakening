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
    NpcCharacter nika = city.Npcs[3];
    Vector3 baseline = mark.Position;

    Require(lida.NarrativeRole == NarrativeRole.Lida && lida.Name == "Лида",
        "city-local second citizen must be Lida");
    Require(mark.NarrativeRole == NarrativeRole.Mark && mark.Name == "Марк",
        "city-local third citizen must be Mark");
    Require(nika.NarrativeRole == NarrativeRole.Nika && nika.Name == "Ника",
        "city-local fourth citizen must be Nika");
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
        "inspection action must exist on morning 1");
    Require(!HasAction(before, FirstMemorySlice.HelpLidaActionId),
        "meeting must be locked on morning 1");

    lida.ApplyChoice(FindAction(before, FirstMemorySlice.InspectSignalActionId), progress);
    Require(FirstMemorySpatial.IsSignalObserved(progress), "signal must become observed");
    Require(!HasAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId),
        "inspection must not unlock meeting before the next morning");

    progress.NewDay();
    _ = detector.Detect(FirstMemorySpatial.SignalPosition); // sync day state
    DialogueChoice help = FindAction(lida.GetDialogueState(0f, progress).choices, FirstMemorySlice.HelpLidaActionId);
    lida.ApplyChoice(help, progress);
    Require(progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out var eventData) &&
            eventData?.LocationId == FirstMemorySlice.LocationId,
        "morning-2 help must create the tram-plaza event");
    Require(Vector3.Distance(mark.Position, lida.Position) < 3f,
        "meeting choice must bring Mark into the meeting space");

    progress.NewDay();
    _ = detector.Detect(FirstMemorySpatial.SignalPosition);
    Require(!progress.Ledger.TryGetEvent(FirstMemorySlice.EventId, out _),
        "unanchored event must be forgotten by Sverka");
    Require(Vector3.Distance(mark.Position, baseline) < 0.01f,
        "unanchored Mark movement must reset to baseline");

    // Separate anchored scenario: the spatial consequence survives into morning 3.
    var anchoredProgress = new HeroProgress();
    var anchoredCity = new CityRenderer(424244, anchoredProgress);
    var anchoredDetector = new InteractionDetector(anchoredCity);
    NpcCharacter anchoredLida = anchoredCity.Npcs[1];
    NpcCharacter anchoredMark = anchoredCity.Npcs[2];

    anchoredLida.ApplyChoice(
        FindAction(anchoredLida.GetDialogueState(0f, anchoredProgress).choices, FirstMemorySlice.InspectSignalActionId),
        anchoredProgress);
    anchoredProgress.NewDay();
    _ = anchoredDetector.Detect(FirstMemorySpatial.SignalPosition);
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
        "anchored event must survive morning-3 Sverka");
    Require(Vector3.Distance(anchoredMark.Position, anchoredLida.Position) < 3f,
        "anchored relationship must remain spatially visible on morning 3");

    string morning = anchoredLida.GetDialogueState(0f, anchoredProgress).npcLine;
    Require(morning.Contains("Марк", StringComparison.Ordinal) &&
            morning.Contains("помню", StringComparison.OrdinalIgnoreCase),
        "morning-3 Lida dialogue must be caused by the persisted event");

    Console.WriteLine("FIRST_MEMORY_SPATIAL_SMOKE=PASS; scenarios=2");
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("FIRST_MEMORY_SPATIAL_SMOKE=FAIL: " + e.Message);
    return 1;
}
