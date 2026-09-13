namespace Probuzhdenie.FreeCity;

public sealed class MemoryWitness
{
    public int NpcId { get; set; }
    public bool UnderstoodEvent { get; set; }
    public bool Consented { get; set; }
    public string ConsentReason { get; set; } = "";
}
