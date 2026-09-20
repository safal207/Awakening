using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;
using Probuzhdenie.Player;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static DialogueChoice FindAction(DialogueChoice[] choices, string actionId)
{
    foreach (DialogueChoice choice in choices)
        if (choice.ActionId == actionId) return choice;
    throw new InvalidOperationException("Missing action: " + actionId);
}

try
{
    for (int i = 0; i < 7; i++)
        _ = new NpcCharacter(Vector3.Zero, Vector3.Zero, 9000 + i);

    const int seed = 424242;
    var progressA = new HeroProgress();
    var cityA = new CityRenderer(seed, progressA);
    var detectorA = new InteractionDetector(cityA); // binds world-local resident ids + chapter spatial roles

    var idsA = new List<int>();
    var uniqueA = new HashSet<int>();
    for (int i = 0; i < cityA.Npcs.Count; i++)
    {
        int persistentId = ResidentIdentity.GetPersistentId(cityA.Npcs[i]);
        idsA.Add(persistentId);
        uniqueA.Add(persistentId);
        Require(persistentId == i, "city A persistent id must equal deterministic resident slot");
    }
    Require(uniqueA.Count == cityA.NpcCount, "all 50 persistent resident ids must be unique in one city");

    NpcCharacter lida = cityA.Npcs[1];
    NpcCharacter mark = cityA.Npcs[2];
    Require(mark.Id != 2, "test setup must actually shift the process-local runtime id");
    Require(ResidentIdentity.GetPersistentId(mark) == 2, "Mark persistent id must remain slot 2");

    lida.ApplyChoice(
        FindAction(lida.GetDialogueState(0f, progressA).choices, FirstMemorySlice.InspectSignalActionId),
        progressA);
    progressA.NewDay();
    _ = detectorA.Detect(FirstMemorySpatial.SignalPosition); // sync spatial morning gate
    lida.ApplyChoice(
        FindAction(lida.GetDialogueState(0f, progressA).choices, FirstMemorySlice.HelpLidaActionId),
        progressA);
    mark.ApplyChoice(
        FindAction(mark.GetDialogueState(0f, progressA).choices, FirstMemorySlice.AcceptWitnessActionId),
        progressA);

    Require(progressA.Ledger.TryGetAnchor(FirstMemorySlice.EventId, out MemoryAnchor? anchor) && anchor != null,
        "First Memory anchor must exist");
    Require(anchor!.Witnesses.ContainsKey(2),
        "MemoryAnchor must store Mark's persistent id, not process-local runtime id");
    Require(!anchor.Witnesses.ContainsKey(mark.Id),
        "shifted process-local runtime id must not leak into persistent memory");

    for (int i = 0; i < 9; i++)
        _ = new NpcCharacter(Vector3.One, Vector3.One, 9100 + i);

    var progressB = new HeroProgress();
    var cityB = new CityRenderer(seed, progressB);
    _ = new InteractionDetector(cityB);

    Require(cityB.NpcCount == idsA.Count, "same seeded city must have same resident count");
    for (int i = 0; i < cityB.Npcs.Count; i++)
        Require(ResidentIdentity.GetPersistentId(cityB.Npcs[i]) == idsA[i],
            "same world seed must reproduce persistent resident ids independent of creation order");

    Console.WriteLine("STABLE_RESIDENT_ID_SMOKE=PASS; residents=50; mark=2");
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("STABLE_RESIDENT_ID_SMOKE=FAIL: " + e.Message);
    return 1;
}
