using System;

namespace Probuzhdenie.FreeCity;

public class AwarenessSystem
{
    public float Level { get; private set; }
    public int Stage => Level switch
    {
        < 15 => 0,
        < 35 => 1,
        < 55 => 2,
        < 80 => 3,
        _ => 4,
    };

    public string[] Messages = {
        "Странно... я делаю это каждый день.",
        "Почему одни и те же лица?",
        "Я вижу повторения... это мир?",
        "Я чувствую, что могу больше!",
        "Я СВОБОДЕН! ВСЁ ПОНАСТОЯЩЕМУ!",
    };

    public string CurrentMessage => Messages[Math.Clamp(Stage, 0, Messages.Length - 1)];

    public void Add(float amount)
    {
        if (Level < 100)
            Level = Math.Min(100, Level + amount);
    }

    public void Restore(float level)
    {
        Level = Math.Clamp(level, 0f, 100f);
    }

    public void Update(NpcCharacter player, float timeOfDay, float dt)
    {
        if (player.State == NpcState.Aware) return;

        if (Stage == 0 && timeOfDay > 12)
            Add(dt * 0.5f);

        if (Stage >= 1)
            Add(dt * 0.2f);

        if (Stage >= 2 && Level > 60)
            Add(dt * 0.4f);

        if (Stage == 3 && Level >= 80)
            Add(dt * 1.0f);

        if (Level >= 100)
        {
            player.State = NpcState.Aware;
            player.Color = new OpenTK.Mathematics.Vector3(0.2f, 0.6f, 1.0f);
        }
    }
}
