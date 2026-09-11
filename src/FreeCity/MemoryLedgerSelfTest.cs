using System;

namespace Probuzhdenie.FreeCity;

public static class MemoryLedgerSelfTest
{
    public static bool Run(out string message)
    {
        try
        {
            var ledger = new MemoryLedger();
            var firstMeeting = new MemoryEvent(
                Id: "first_memory_lida_mark_meeting",
                Day: 2,
                Kind: "shared_meeting",
                LocationId: "tram_plaza",
                ActorId: 0,
                ChoiceId: "leave_signal_repair_to_help_lida",
                Description: "Lida delays the tram and meets Mark in the plaza.");

            Require(ledger.TryRecordEvent(firstMeeting), "valid event must be recorded");
            Require(!ledger.TryRecordEvent(firstMeeting), "duplicate event ID must be idempotent");
            Require(!ledger.IsPersisted(firstMeeting.Id), "event alone must not survive Sverka");

            Require(!ledger.TryCreateAnchor(firstMeeting.Id, "dispatcher_note", witnessNpcId: 0, WitnessConsent.Accepted),
                "actor cannot witness their own event");
            Require(!ledger.TryCreateAnchor(firstMeeting.Id, "dispatcher_note", witnessNpcId: 12, WitnessConsent.Unknown),
                "unknown consent must not create an anchor");
            Require(!ledger.TryCreateAnchor(firstMeeting.Id, "dispatcher_note", witnessNpcId: 12, WitnessConsent.Declined),
                "declined consent must not create an anchor");
            Require(!ledger.TryCreateAnchor(firstMeeting.Id, "", witnessNpcId: 12, WitnessConsent.Accepted),
                "trace is required");
            Require(!ledger.TryCreateAnchor("missing_event", "dispatcher_note", witnessNpcId: 12, WitnessConsent.Accepted),
                "anchor requires an existing event");

            Require(ledger.TryCreateAnchor(firstMeeting.Id, "dispatcher_note", witnessNpcId: 12, WitnessConsent.Accepted),
                "accepted independent witness plus trace creates an anchor");
            Require(ledger.IsPersisted(firstMeeting.Id), "anchored event must be marked persisted");
            Require(!ledger.TryCreateAnchor(firstMeeting.Id, "another_trace", witnessNpcId: 13, WitnessConsent.Accepted),
                "same event cannot be anchored twice");

            Require(ledger.TryGetAnchor(firstMeeting.Id, out var anchor) && anchor != null,
                "created anchor must be retrievable");
            Require(anchor!.TraceId == "dispatcher_note", "anchor trace must remain stable");
            Require(anchor.Witnesses.TryGetValue(12, out var consent) && consent == WitnessConsent.Accepted,
                "accepted witness must be preserved");

            var invalid = new MemoryEvent("", 0, "", "", 0, "", "");
            Require(!ledger.TryRecordEvent(invalid), "invalid event must be rejected");

            message = "Memory ledger self-test passed.";
            return true;
        }
        catch (Exception e)
        {
            message = "Memory ledger self-test failed: " + e.Message;
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
