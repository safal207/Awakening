using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;

namespace Probuzhdenie.Player;

public enum InteractionType
{
    None,
    Talk,
    Finding,
    Enter,
    Exit,
}

public readonly struct InteractionResult
{
    public InteractionType Type { get; }
    public string Prompt { get; }
    public NpcCharacter? TargetNpc { get; }
    public string? FindingId { get; }

    public InteractionResult(
        InteractionType type,
        string prompt,
        NpcCharacter? targetNpc = null,
        string? findingId = null)
    {
        Type = type;
        Prompt = prompt;
        TargetNpc = targetNpc;
        FindingId = findingId;
    }
}

public sealed class InteractionDetector
{
    private readonly CityRenderer _city;
    private const float InteractionRange = 3f;

    public InteractionDetector(CityRenderer city)
    {
        _city = city;
        ResidentIdentity.BindCity(city.Npcs);
        NpcAwakeningPersistence.RegisterCity(city);
        PlayerSpatialPersistence.RegisterCity(city);
        FirstMemorySpatial.ConfigureCity(city);
    }

    public InteractionResult Detect(Vector3 playerPos)
    {
        if (_city.IsInside)
            return new InteractionResult(InteractionType.Exit, "[E] Выйти");

        var signalTarget = FirstMemorySpatial.SignalInteractionTarget(_city, playerPos, InteractionRange);
        if (signalTarget != null)
            return new InteractionResult(InteractionType.Talk, "[E] Осмотреть сигнал", signalTarget);

        if (FirstMemoryFindings.TryFindNearby(
                _city.Progress,
                playerPos,
                InteractionRange,
                out StoryFinding? finding) &&
            finding != null)
        {
            return new InteractionResult(
                InteractionType.Finding,
                $"[E] Осмотреть: {finding.Title}",
                findingId: finding.Id);
        }

        var npc = _city.FindClosestNpc(playerPos, InteractionRange);
        if (npc != null)
            return new InteractionResult(InteractionType.Talk, "[E] Говорить", npc);

        if (_city.IsNearDoor(playerPos, InteractionRange))
            return new InteractionResult(InteractionType.Enter, "[E] Войти");

        return new InteractionResult(InteractionType.None, "");
    }
}
