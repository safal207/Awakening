namespace Probuzhdenie.FreeCity;

public static class MemoryRuntime
{
    private static MemoryLedger _current = new();

    public static MemoryLedger Current => _current;
    public static int HeroId { get; set; }
    public static SverkaReport? LastSverkaReport { get; private set; }

    public static void Replace(MemoryLedger? ledger)
    {
        _current = ledger ?? new MemoryLedger();
        LastSverkaReport = null;
    }

    public static SverkaReport RunSverka(int day)
    {
        LastSverkaReport = SverkaEngine.Run(_current, HeroId, day);
        return LastSverkaReport;
    }

    public static void Reset()
    {
        _current = new MemoryLedger();
        HeroId = 0;
        LastSverkaReport = null;
    }
}
