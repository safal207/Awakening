using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public static class MemoryAnchorTests
{
    public static bool Run(out string message)
    {
        var accepted = new MemoryLedger();
        var acceptedEvent = new MemoryEvent
        {
            EventId = "tram-stop-meeting-day1",
            EventType = "shared_meeting",
            ParticipantIds = new List<int> { 0, 12, 21 },
            LocationId = "tram-stop-4",
            ChoiceId = "delay_tram_for_mark",
            Day = 1,
            Consequence = "Lida met Mark",
            HadAlternativeChoice = true,
        };
        var acceptedAnchor = new MemoryAnchor
        {
            EventId = acceptedEvent.EventId,
            TraceId = "dispatcher-note-1",
            Witnesses = new List<MemoryWitness>
            {
                new() { NpcId = 0, UnderstoodEvent = true, Consented = true },
                new() { NpcId = 12, UnderstoodEvent = true, Consented = true, ConsentReason = "Lida chose to remember" },
            },
        };
        bool firstEvent = accepted.RegisterEvent(acceptedEvent);
        bool duplicateEvent = accepted.RegisterEvent(acceptedEvent);
        bool firstAnchor = accepted.RegisterAnchor(acceptedAnchor);
        bool persisted = accepted.ResolveForSverka(acceptedEvent.EventId, 0, out string persistedReason);

        var refused = new MemoryLedger();
        var refusedEvent = new MemoryEvent
        {
            EventId = "tram-stop-no-consent-day1",
            EventType = "shared_meeting",
            ParticipantIds = new List<int> { 0, 12 },
            LocationId = "tram-stop-4",
            ChoiceId = "delay_tram_for_mark",
            Day = 1,
            Consequence = "Lida saw Mark",
            HadAlternativeChoice = true,
        };
        var refusedAnchor = new MemoryAnchor
        {
            EventId = refusedEvent.EventId,
            TraceId = "dispatcher-note-2",
            Witnesses = new List<MemoryWitness>
            {
                new() { NpcId = 0, UnderstoodEvent = true, Consented = true },
                new() { NpcId = 12, UnderstoodEvent = true, Consented = false, ConsentReason = "Lida refused" },
            },
        };
        refused.RegisterEvent(refusedEvent);
        refused.RegisterAnchor(refusedAnchor);
        bool refusedPersisted = refused.ResolveForSverka(refusedEvent.EventId, 0, out string refusedReason);
        refusedAnchor.Witnesses[1].Consented = true;
        bool refusedReplayPersisted = refused.ResolveForSverka(refusedEvent.EventId, 0, out string refusedReplayReason);

        var outsider = new MemoryLedger();
        outsider.RegisterEvent(new MemoryEvent
        {
            EventId = "outsider-witness",
            EventType = "shared_meeting",
            ParticipantIds = new List<int> { 0, 12 },
            LocationId = "tram-stop-4",
            ChoiceId = "delay_tram_for_mark",
            Day = 1,
            Consequence = "Lida met Mark",
            HadAlternativeChoice = true,
        });
        outsider.RegisterAnchor(new MemoryAnchor
        {
            EventId = "outsider-witness",
            TraceId = "outsider-note",
            Witnesses = new List<MemoryWitness>
            {
                new() { NpcId = 99, UnderstoodEvent = true, Consented = true },
            },
        });
        bool outsiderPersisted = outsider.ResolveForSverka("outsider-witness", 0, out string outsiderReason);

        bool ok = firstEvent && !duplicateEvent && firstAnchor && persisted &&
                  accepted.HasPersisted(acceptedEvent.EventId) &&
                  persistedReason == "event+participant+witness+understanding+consent+trace+choice" &&
                  !refusedPersisted && !refusedReplayPersisted &&
                  refusedReason == "missing_voluntary_participant_witness" &&
                  refusedReplayReason == refusedReason &&
                  !outsiderPersisted && outsiderReason == "missing_voluntary_participant_witness";

        message = ok
            ? "Memory anchor tests passed."
            : $"Memory anchor tests failed: accepted={persisted} ({persistedReason}), refused={refusedPersisted}/{refusedReplayPersisted} ({refusedReason}/{refusedReplayReason}), outsider={outsiderPersisted} ({outsiderReason}).";
        return ok;
    }
}
