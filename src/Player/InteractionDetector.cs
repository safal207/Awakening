using OpenTK.Mathematics;
using Probuzhdenie.FreeCity;

namespace Probuzhdenie.Player;

public enum InteractionType
{
    None,
    Talk,
    Enter,
    Exit,
}

public readonly struct InteractionResult
{
    public InteractionType Type { get; }
    public string Prompt { get; }
    public NpcCharacter? TargetNpc { get; }

    public InteractionResult(InteractionType type, string prompt, NpcCharacter? targetNpc = null)
    {
        Type = type;
        Prompt = prompt;
        TargetNpc = targetNpc;
    }
}

public sealed class InteractionDetector
{
    private readonly CityRenderer _city;
    private const float InteractionRange = 3f;

    public InteractionDetector(CityRenderer city)
    {
        _city = city;
    }

    public InteractionResult Detect(Vector3 playerPos)
    {
        if (_city.IsInside)
            return new InteractionResult(InteractionType.Exit, "[E] Выйти");

        var npc = _city.FindClosestNpc(playerPos, InteractionRange);
        if (npc != null)
            return new InteractionResult(InteractionType.Talk, "[E] Говорить", npc);

        if (_city.IsNearDoor(playerPos, InteractionRange))
            return new InteractionResult(InteractionType.Enter, "[E] Войти");

        return new InteractionResult(InteractionType.None, "");
    }
}
