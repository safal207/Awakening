using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public static class SverkaEngineTests
{
    public static bool Run(out string message)
    {
        var ledger = new MemoryLedger();

        AddCase(ledger, "day1-persist", 1, 12, true);
        AddCase(ledger, "day1-refuse", 1, 13, false);
        AddCase(ledger, "day2-future", 2, 14, true);

        var day1 = SverkaEngine.Run(ledger, heroId: 0, day: 1);
        var repeatedDay1 = SverkaEngine.Run(ledger, heroId: 0, day: 1);
        var day2 = SverkaEngine.Run(ledger, heroId: 0, day: 2);

        bool ok =
            day1.Decisions.Count == 2 &&
            day1.PersistedCount == 1 &&
            day1.RejectedCount == 1 &&
            repeatedDay1.Decisions.Count == 0 &&
            day2.Decisions.Count == 1 &&
            day2.PersistedCount == 1 &&
            ledger.HasPersisted("day1-persist") &&
            !ledger.HasPersisted("day1-refuse") &&
            ledger.HasPersisted("day2-future");

        message = ok
            ? "Sverka engine tests passed."
            : $"Sverka engine tests failed: day1={day1.Decisions.Count}/{day1.PersistedCount}/{day1.RejectedCount}, repeat={repeatedDay1.Decisions.Count}, day2={day2.Decisions.Count}/{day2.PersistedCount}.";
        return ok;
    }

    private static void AddCase(MemoryLedger ledger, string eventId, int day, int witnessId, bool consented)
    {
        ledger.RegisterEvent(new MemoryEvent
        {
            EventId = eventId,
            EventType = "test",
            ParticipantIds = new List<int> { 0, witnessId },
            LocationId = "test-location",
            ChoiceId = "test-choice",
            Day = day,
            Consequence = "observable change",
            HadAlternativeChoice = true,
        });
        ledger.RegisterAnchor(new MemoryAnchor
        {
            EventId = eventId,
            TraceId = eventId + "-trace",
            Witnesses = new List<MemoryWitness>
            {
                new() { NpcId = witnessId, UnderstoodEvent = true, Consented = consented },
            },
        });
    }
}
