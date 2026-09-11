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
                new() { NpcId = 0, Consented = true },
                new() { NpcId = 12, Consented = true, ConsentReason = "Lida chose to remember" },
            },
        };

        bool firstEvent = accepted.RegisterEvent(acceptedEvent);
        bool duplicateEvent = accepted.RegisterEvent(acceptedEvent);
        bool firstAnchor = accepted.RegisterAnchor(acceptedAnchor);
        bool persisted = accepted.ResolveForSverka(acceptedEvent.EventId, 0, out string persistedReason);

        var rejected = new MemoryLedger();
        var rejectedEvent = new MemoryEvent
        {
            EventId = "tram-stop-no-consent-day1",
            EventType = "shared_meeting",
            LocationId = "tram-stop-4",
            ChoiceId = "delay_tram_for_mark",
            Day = 1,
            Consequence = "Lida saw Mark",
            HadAlternativeChoice = true,
        };
        var rejectedAnchor = new MemoryAnchor
        {
            EventId = rejectedEvent.EventId,
            TraceId = "dispatcher-note-2",
            Witnesses = new List<MemoryWitness>
            {
                new() { NpcId = 0, Consented = true },
                new() { NpcId = 12, Consented = false, ConsentReason = "Lida refused" },
            },
        };
        rejected.RegisterEvent(rejectedEvent);
        rejected.RegisterAnchor(rejectedAnchor);
        bool rejectedPersisted = rejected.ResolveForSverka(rejectedEvent.EventId, 0, out string rejectedReason);

        bool ok = firstEvent && !duplicateEvent && firstAnchor && persisted &&
                  accepted.HasPersisted(acceptedEvent.EventId) &&
                  persistedReason == "event+witness+consent+trace+choice" &&
                  !rejectedPersisted && !rejected.HasPersisted(rejectedEvent.EventId) &&
                  rejectedReason == "missing_voluntary_witness";

        message = ok
            ? "Memory anchor tests passed."
            : $"Memory anchor tests failed: accepted={persisted} ({persistedReason}), rejected={rejectedPersisted} ({rejectedReason}), duplicateBlocked={!duplicateEvent}.";
        return ok;
    }
}
