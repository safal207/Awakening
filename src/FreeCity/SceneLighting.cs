using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public readonly record struct SceneLighting(Vector3 Sky, Vector3 Ambient, Vector3 Sun, float WindowGlow)
{
    public static SceneLighting At(float hour)
    {
        if (!float.IsFinite(hour)) hour = 12;
        hour = (hour % 24 + 24) % 24;
        float angle = (hour - 6) / 12 * MathF.PI;
        float elevation = MathF.Sin(angle);
        float day = Math.Clamp(elevation, 0, 1);
        float twilight = Math.Clamp(1 - MathF.Abs(elevation) * 3, 0, 1);
        Vector3 sky = Vector3.Lerp(new(0.045f,0.065f,0.10f),new(0.66f,0.78f,0.82f),MathF.Sqrt(day));
        sky = Vector3.Lerp(sky,new(0.72f,0.56f,0.46f),twilight*0.30f);
        Vector3 ambient = Vector3.Lerp(new(0.24f,0.27f,0.33f),new(0.65f,0.66f,0.64f),day);
        Vector3 sun = new(MathF.Cos(angle)*0.65f, Math.Max(0.18f,elevation),0.35f);
        return new(sky,ambient,sun,1-Math.Clamp(day*3,0,1));
    }
}
