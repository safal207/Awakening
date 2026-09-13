using System;
using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public static class MemoryLedgerSelfTest
{
    public static bool Run(out string message)
    {
        try
        {
            var ledger = new MemoryLedger();
            var meeting = Event("meeting");
            Require(ledger.RegisterEvent(meeting), "record valid event");
            Require(!ledger.RegisterEvent(meeting), "deduplicate event identity");
            Require(!ledger.RegisterEvent(new MemoryEvent()), "reject incomplete event");
            var invalidDay = Event("invalid-day");
            invalidDay.Day = 0;
            Require(!ledger.RegisterEvent(invalidDay), "reject invalid day");
            Require(!ledger.RegisterAnchor(new MemoryAnchor { EventId = "missing" }), "reject orphan anchor");

            var anchor = Anchor(meeting.EventId, 12, true, true);
            Require(ledger.RegisterAnchor(anchor), "record candidate anchor");
            Require(!ledger.HasPersisted(meeting.EventId), "candidate must await Sverka");
            Require(!ledger.RegisterAnchor(Anchor(meeting.EventId, 21, true, true)), "deduplicate anchor identity");
            Require(ledger.ResolveForSverka(meeting.EventId, 0, out _), "preserve informed voluntary witness");
            Require(ledger.FindEvent(meeting.EventId) == meeting && ledger.FindAnchor(meeting.EventId) == anchor,
                "lookup uses canonical event identity");
            Require(anchor.TraceId == "dispatcher-note", "preserve original trace");

            foreach (var test in new[]
            {
                (Id: "self", Witness: 0, Understood: true, Consent: true, Trace: "note", Reason: "missing_voluntary_participant_witness"),
                (Id: "unknown", Witness: 12, Understood: false, Consent: true, Trace: "note", Reason: "missing_voluntary_participant_witness"),
                (Id: "declined", Witness: 12, Understood: true, Consent: false, Trace: "note", Reason: "missing_voluntary_participant_witness"),
                (Id: "no-trace", Witness: 12, Understood: true, Consent: true, Trace: "", Reason: "missing_trace"),
            })
            {
                ledger.RegisterEvent(Event(test.Id));
                var candidate = Anchor(test.Id, test.Witness, test.Understood, test.Consent);
                candidate.TraceId = test.Trace;
                ledger.RegisterAnchor(candidate);
                Require(!ledger.ResolveForSverka(test.Id, 0, out string reason) && reason == test.Reason,
                    "reject invalid persistence: " + test.Id);
            }

            message = "Memory ledger self-test passed.";
            return true;
        }
        catch (Exception e)
        {
            message = "Memory ledger self-test failed: " + e.Message;
            return false;
        }
    }

    private static MemoryEvent Event(string id) => new()
    {
        EventId = id, Day = 1, EventType = "shared_meeting", LocationId = "tram-plaza",
        ParticipantIds = new List<int> { 0, 12, 21 }, ChoiceId = "delay-tram",
        Consequence = "Lida met Mark", HadAlternativeChoice = true,
    };

    private static MemoryAnchor Anchor(string id, int witness, bool understood, bool consent) => new()
    {
        EventId = id, TraceId = "dispatcher-note",
        Witnesses = new List<MemoryWitness> { new() { NpcId = witness, UnderstoodEvent = understood, Consented = consent } },
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
