using System.Collections.Generic;

namespace Probuzhdenie.FreeCity;

public sealed class MemoryEvent
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public List<int> ParticipantIds { get; set; } = new();
    public string LocationId { get; set; } = "";
    public string ChoiceId { get; set; } = "";
    public int Day { get; set; }
    public string Consequence { get; set; } = "";
    public bool HadAlternativeChoice { get; set; } = true;
}
