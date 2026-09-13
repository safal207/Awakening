using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

// Normalized body coordinates; the mesh applies the character height and yaw.
public static class HeroPose
{
    public const float LegLength = 0.235f;
    public const float AnkleHeight = 0.047f;
    public static float BodyOffset(float blend) => -0.018f * blend;

    public static CharacterPose Create(float phase, float blend)
    {
        if (!float.IsFinite(phase)) phase = 0;
        blend = float.IsFinite(blend) ? Math.Clamp(blend, 0, 1) : 0;
        float swing = MathF.Cos(phase), lift = MathF.Sin(phase);
        return new CharacterPose(Leg(-1, swing, lift, blend), Leg(1, -swing, -lift, blend),
            Arm(-1, -swing, blend), Arm(1, swing, blend));
    }

    private static LimbPose Leg(float side, float swing, float lift, float blend)
    {
        Vector3 hip = new(side * 0.054f, 0.511f + BodyOffset(blend), 0);
        Vector3 ankle = new(hip.X, AnkleHeight + Math.Max(0, lift) * 0.055f * blend,
            swing * 0.12f * blend);
        Vector3 delta = ankle - hip;
        Vector3 axis = delta.Normalized();
        Vector3 bend = (Vector3.UnitZ - axis * axis.Z).Normalized();
        float kneeOffset = MathF.Sqrt(Math.Max(0, LegLength * LegLength - delta.LengthSquared * 0.25f));
        return new(hip, (hip + ankle) * 0.5f + bend * kneeOffset, ankle);
    }

    private static LimbPose Arm(float side, float swing, float blend)
    {
        Vector3 shoulder = new(side * 0.117f, 0.790f + BodyOffset(blend), 0);
        float angle = swing * 0.29f * blend - 0.045f;
        Vector3 Down(float length, float a, float outward) =>
            new Vector3(side * outward, -MathF.Cos(a), MathF.Sin(a)).Normalized() * length;
        Vector3 elbow = shoulder + Down(0.166f, angle, 0.06f);
        return new(shoulder, elbow, elbow + Down(0.145f, angle + 0.12f, 0.025f));
    }
}
