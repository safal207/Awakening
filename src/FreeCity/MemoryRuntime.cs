namespace Probuzhdenie.FreeCity;

public static class MemoryRuntime
{
    private static MemoryLedger _current = new();

    public static MemoryLedger Current => _current;

    public static void Replace(MemoryLedger? ledger)
    {
        _current = ledger ?? new MemoryLedger();
    }

    public static void Reset()
    {
        _current = new MemoryLedger();
    }
}
