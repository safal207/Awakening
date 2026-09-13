using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public readonly record struct LimbPose(Vector3 Root, Vector3 Joint, Vector3 Tip);

public enum CharacterDetail { Full, Reduced, Silhouette }

public readonly record struct CharacterPose(LimbPose LeftLeg, LimbPose RightLeg, LimbPose LeftArm, LimbPose RightArm)
{
    public static CharacterPose Create(float phase, float blend, float greeting = 0f)
    {
        if (!float.IsFinite(phase)) phase = 0;
        if (!float.IsFinite(blend)) blend = 0;
        blend = Math.Clamp(blend, 0f, 1f);
        float swing = MathF.Sin(phase);
        var right = Arm(1, swing, blend);
        greeting = float.IsFinite(greeting) ? Math.Clamp(greeting, 0, 1) : 0;
        if (greeting > 0)
        {
            Vector3 upper = Vector3.Lerp((right.Joint-right.Root).Normalized(),new Vector3(0.75f,0.4f,0.5f).Normalized(),greeting).Normalized();
            Vector3 lower = Vector3.Lerp((right.Tip-right.Joint).Normalized(),new Vector3(0.1f,0.95f,0.15f).Normalized(),greeting).Normalized();
            Vector3 elbow = right.Root + upper * 0.163f;
            right = new LimbPose(right.Root,elbow,elbow + lower * 0.148f);
        }
        return new CharacterPose(Leg(-1, swing, blend), Leg(1, -swing, blend), Arm(-1, -swing, blend), right);
    }

    private static LimbPose Leg(float side, float swing, float blend)
    {
        Vector3 hip = new(side * 0.057f, 0.515f, 0);
        float thighAngle = swing * 0.40f * blend;
        float kneeAngle = thighAngle - Math.Max(0, -swing) * 0.65f * blend;
        Vector3 knee = hip + Down(0.235f, thighAngle);
        return new LimbPose(hip, knee, knee + Down(0.235f, kneeAngle));
    }

    private static LimbPose Arm(float side, float swing, float blend)
    {
        Vector3 shoulder = new(side * 0.130f, 0.781f, 0);
        float upperAngle = swing * 0.38f * blend - 0.03f;
        Vector3 elbow = shoulder + Down(0.163f, upperAngle, side * 0.055f);
        return new LimbPose(shoulder, elbow, elbow + Down(0.148f, upperAngle + 0.13f, side * 0.025f));
    }

    private static Vector3 Down(float length, float angle, float outward = 0) =>
        new Vector3(outward, -MathF.Cos(angle), MathF.Sin(angle)).Normalized() * length;
}

// CPU-only geometry, shared by the city, menu portrait and headless tests.
public sealed partial class CharacterMesh
{
    public const int FloatsPerVertex = 9;
    private readonly record struct SurfacePoint(Vector3 Position, Vector3 Normal);
    private static readonly Vector2[] Circle = MakeCircle(12);
    private static readonly Vector2[] LowCircle = MakeCircle(6);
    private static readonly Vector3[] Sphere = MakeSphere(8, 6);
    private static readonly Vector3[] HeadSphere = MakeSphere(12, 8);
    private static readonly Vector3[] LowSphere = MakeSphere(6, 4);
    private static readonly Vector3[] FarSphere = MakeSphere(5, 3);
    private Vector2[] _circle = Circle;
    private CharacterDetail _detail;
    private float[] _data = new float[65536];
    private Vector3 _origin;
    private float _height, _sin, _cos;

    public float[] Data => _data;
    public int FloatCount { get; private set; }
    public int VertexCount => FloatCount / FloatsPerVertex;
    public void Clear() => FloatCount = 0;

    public static CharacterDetail DetailForDistance(float squaredDistance) =>
        squaredDistance < 12 * 12 ? CharacterDetail.Full :
        squaredDistance < 35 * 35 ? CharacterDetail.Reduced : CharacterDetail.Silhouette;

    public void Append(NpcCharacter npc, float time, Vector3? position = null, float? rotation = null,
        float? movement = null, CharacterDetail detail = CharacterDetail.Full, bool showAwareness = true)
    {
        _detail = detail;
        _circle = detail == CharacterDetail.Full ? Circle : LowCircle;
        _origin = position ?? npc.Position;
        _height = npc.Height;
        float yaw = rotation ?? npc.Rotation;
        if (!Finite(_origin) || !float.IsFinite(yaw) || !float.IsFinite(_height) || _height <= 0) return;
        _sin = MathF.Sin(yaw);
        _cos = MathF.Cos(yaw);
        float blend = Math.Clamp(movement ?? npc.AnimBlend, 0, 1);
        if (!float.IsFinite(blend)) blend = 0;
        if (!float.IsFinite(time)) time = 0;
        if (HeroStyle.IsHero(npc))
        {
            AppendHero(npc, time, blend);
            if (showAwareness && npc.State == NpcState.Aware) AwarenessMarker();
            return;
        }
        CharacterPose pose = CharacterPose.Create(npc.AnimPhase, blend, npc.Greeting);
        float breath = MathF.Sin(time * 1.8f) * 0.0015f * (1f - blend);
        Vector3 skin = npc.HeadColor;
        Vector3 shirt = npc.Color;
        Vector3 pants = npc.PantsColor;
        Vector3 hair = npc.HairColor;

        BuildLeg(pose.LeftLeg, pants);
        BuildLeg(pose.RightLeg, pants);
        Ellipsoid(new(0, 0.515f, 0), new(0.111f, 0.064f, 0.067f), pants);

        // An elliptical torso keeps the chest broad from the front and shallow in profile.
        TorsoSection(0.55f, 0.63f, new(0.101f, 0.068f), new(0.094f, 0.063f), shirt * 0.94f, breath);
        TorsoSection(0.63f, 0.755f, new(0.094f, 0.063f), new(0.124f, 0.076f), shirt, breath);
        TorsoSection(0.755f, 0.808f, new(0.124f, 0.076f), new(0.102f, 0.060f), shirt, breath);
        Ellipsoid(new(0, 0.796f + breath, 0), new(0.118f, 0.030f, 0.064f), shirt);
        TorsoSection(0.798f, 0.838f, new(0.102f, 0.060f), new(0.033f, 0.032f), shirt, breath);
        TorsoSection(0.549f, 0.565f, new(0.104f, 0.070f), new(0.103f, 0.070f), HeroStyle.Belt);
        Patch(new(-0.014f, 0.549f, 0.071f), new(0.014f, 0.549f, 0.071f),
            new(0.014f, 0.565f, 0.071f), new(-0.014f, 0.565f, 0.071f), new(0.64f, 0.66f, 0.65f));

        BuildArm(pose.LeftArm, shirt, skin);
        BuildArm(pose.RightArm, shirt, skin);
        Tube(new(0, 0.793f + breath, 0), new(0, 0.871f + breath, 0), 0.031f, 0.029f, skin);

        // Tiny clothing and face features are only useful at conversational distance.
        if (_detail == CharacterDetail.Full)
        {
            Patch(new(-0.034f, 0.818f + breath, 0.032f), new(-0.058f, 0.795f + breath, 0.058f),
                new(-0.028f, 0.774f + breath, 0.077f), new(-0.005f, 0.798f + breath, 0.061f), shirt * 1.12f);
            Patch(new(0.005f, 0.798f + breath, 0.061f), new(0.028f, 0.774f + breath, 0.077f),
                new(0.058f, 0.795f + breath, 0.058f), new(0.034f, 0.818f + breath, 0.032f), shirt * 1.12f);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.602f + i * 0.043f + breath;
                float z = 0.065f + (y - 0.63f) * 0.105f;
                Ellipsoid(new(0, y, z + 0.002f), new(0.003f, 0.003f, 0.002f), new(0.8f, 0.86f, 0.85f));
            }
            Patch(new(-0.080f, 0.702f + breath, 0.060f), new(-0.036f, 0.702f + breath, 0.074f),
                new(-0.036f, 0.738f + breath, 0.077f), new(-0.080f, 0.738f + breath, 0.062f), shirt * 0.91f);
        }

        Vector3 head = new(0, 0.928f + breath, 0);
        Ellipsoid(head, new(0.052f, 0.067f, 0.047f), skin, detailed: true);
        if (_detail == CharacterDetail.Full)
        {
            Ellipsoid(head + new Vector3(-0.052f, -0.002f, 0), new(0.008f, 0.017f, 0.011f), skin * 0.97f);
            Ellipsoid(head + new Vector3(0.052f, -0.002f, 0), new(0.008f, 0.017f, 0.011f), skin * 0.97f);
            Ellipsoid(head + new Vector3(0, -0.005f, 0.044f), new(0.006f, 0.014f, 0.009f), skin);
            Ellipsoid(head + new Vector3(0, -0.013f, 0.051f), new(0.008f, 0.006f, 0.007f), skin * 0.96f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.019f;
                Ellipsoid(head + new Vector3(x, 0.009f, 0.042f), new(0.010f, 0.0055f, 0.004f), new(0.91f, 0.91f, 0.85f));
                Ellipsoid(head + new Vector3(x, 0.009f, 0.046f), new(0.0035f, 0.0038f, 0.0016f), HeroStyle.Eye);
                Ellipsoid(head + new Vector3(x, 0.019f, 0.041f), new(0.011f, 0.002f, 0.002f), hair);
            }
            Ellipsoid(head + new Vector3(0, -0.028f, 0.043f), new(0.013f, 0.0025f, 0.002f), new(0.48f, 0.27f, 0.23f));
        }
        HairCap(breath, hair);
        if (showAwareness && HeroStyle.IsHero(npc) && npc.State == NpcState.Aware)
            AwarenessMarker();
    }

    private void AwarenessMarker()
    {
        Vector3 color = new(0.18f, 0.58f, 1);
        for (int i = 0; i < Circle.Length; i++)
        {
            Vector2 a = Circle[i], b = Circle[(i + 1) % Circle.Length];
            SurfacePoint Point(Vector2 p, float radius) =>
                new(new Vector3(p.X * radius, 0.018f, p.Y * radius), Vector3.UnitY);
            Triangle(Point(a, 0.32f), Point(b, 0.32f), Point(b, 0.34f), color);
            Triangle(Point(a, 0.32f), Point(b, 0.34f), Point(a, 0.34f), color);
        }
        Ellipsoid(new Vector3(0, 1.045f, 0), new Vector3(0.022f), color);
    }

    private void BuildLeg(LimbPose leg, Vector3 pants)
    {
        Tube(leg.Root, leg.Joint, 0.051f, 0.038f, pants);
        Ellipsoid(leg.Joint, new(0.039f, 0.040f, 0.039f), pants);
        Tube(leg.Joint, leg.Tip, 0.038f, 0.030f, pants);
        Ellipsoid(leg.Tip + new Vector3(0, -0.018f, 0.023f), new(0.035f, 0.027f, 0.070f), HeroStyle.Shoe);
    }

    private void BuildArm(LimbPose arm, Vector3 shirt, Vector3 skin)
    {
        Vector3 cuff = Vector3.Lerp(arm.Root, arm.Joint, 0.9f);
        Ellipsoid(arm.Root, new(0.033f, 0.033f, 0.035f), shirt);
        Tube(arm.Root, cuff, 0.033f, 0.028f, shirt);
        Tube(Vector3.Lerp(arm.Root, arm.Joint, 0.80f), cuff, 0.031f, 0.031f, shirt * 1.1f);
        Ellipsoid(arm.Joint, new(0.027f), skin);
        Tube(cuff, arm.Joint, 0.027f, 0.027f, skin);
        Tube(arm.Joint, arm.Tip, 0.027f, 0.020f, skin);
        Ellipsoid(arm.Tip + new Vector3(0, -0.021f, 0.003f), new(0.024f, 0.033f, 0.017f), skin);
        if (_detail == CharacterDetail.Full)
        {
            float side = Math.Sign(arm.Root.X);
            Ellipsoid(arm.Tip + new Vector3(-side * 0.020f, -0.014f, 0.009f), new(0.009f, 0.020f, 0.009f), skin);
        }
    }

    private void Ellipsoid(Vector3 center, Vector3 radius, Vector3 color, bool detailed = false, bool small = false)
    {
        Vector3[] sphere = _detail switch
        {
            CharacterDetail.Reduced => LowSphere,
            CharacterDetail.Silhouette => FarSphere,
            _ => small ? LowSphere : detailed ? HeadSphere : Sphere,
        };
        for (int i = 0; i < sphere.Length; i += 3)
        {
            SurfacePoint Point(Vector3 unit) => new(center + unit * radius, (unit / radius).Normalized());
            Triangle(Point(sphere[i]), Point(sphere[i + 1]), Point(sphere[i + 2]), color);
        }
    }

    private void Tube(Vector3 start, Vector3 end, float startRadius, float endRadius, Vector3 color)
    {
        Vector3 delta = end - start;
        float length = delta.Length;
        if (length < 0.0001f) return;
        Vector3 axis = delta / length;
        Vector3 basis = Math.Abs(axis.X) < 0.9f ? Vector3.UnitX : Vector3.UnitZ;
        Vector3 u = (basis - axis * Vector3.Dot(basis, axis)).Normalized();
        Vector3 v = Vector3.Cross(u, axis);
        float slope = (startRadius - endRadius) / length;
        for (int i = 0; i < _circle.Length; i++)
        {
            Vector2 a = _circle[i], b = _circle[(i + 1) % _circle.Length];
            Vector3 ra = u * a.X + v * a.Y, rb = u * b.X + v * b.Y;
            Vector3 na = (ra + axis * slope).Normalized(), nb = (rb + axis * slope).Normalized();
            SurfacePoint sa = new(start + ra * startRadius, na), sb = new(start + rb * startRadius, nb);
            SurfacePoint ea = new(end + ra * endRadius, na), eb = new(end + rb * endRadius, nb);
            Triangle(sa, ea, eb, color);
            Triangle(sa, eb, sb, color);
            Triangle(new(start, -axis), new(sa.Position, -axis), new(sb.Position, -axis), color);
            Triangle(new(end, axis), new(eb.Position, axis), new(ea.Position, axis), color);
        }
    }

    private void TorsoSection(float bottom, float top, Vector2 lower, Vector2 upper, Vector3 color, float breath = 0)
    {
        Vector3 low = new(0, bottom + breath, 0), high = new(0, top + breath, 0);
        for (int i = 0; i < _circle.Length; i++)
        {
            Vector2 a = _circle[i], b = _circle[(i + 1) % _circle.Length];
            Vector3 Normal(Vector2 p) => new Vector3(p.X / upper.X,
                ((lower.X - upper.X) / upper.X + (lower.Y - upper.Y) / upper.Y) / (2 * (top - bottom)), p.Y / upper.Y).Normalized();
            SurfacePoint la = new(low + new Vector3(a.X * lower.X, 0, a.Y * lower.Y), Normal(a));
            SurfacePoint lb = new(low + new Vector3(b.X * lower.X, 0, b.Y * lower.Y), Normal(b));
            SurfacePoint ha = new(high + new Vector3(a.X * upper.X, 0, a.Y * upper.Y), Normal(a));
            SurfacePoint hb = new(high + new Vector3(b.X * upper.X, 0, b.Y * upper.Y), Normal(b));
            Triangle(la, ha, hb, color);
            Triangle(la, hb, lb, color);
            Triangle(new(low, -Vector3.UnitY), new(la.Position, -Vector3.UnitY), new(lb.Position, -Vector3.UnitY), color);
            Triangle(new(high, Vector3.UnitY), new(hb.Position, Vector3.UnitY), new(ha.Position, Vector3.UnitY), color);
        }
    }

    private void HairCap(float breath, Vector3 color)
    {
        int rings = _detail == CharacterDetail.Full ? 5 : 3;
        Vector3 center = new(0, 0.943f + breath, 0);
        Vector3 radius = new(0.054f, 0.055f, 0.050f);
        SurfacePoint Point(int ring, Vector2 p)
        {
            float angle = (1.5f - 0.35f * p.Y) * ring / rings;
            Vector3 unit = new(p.X * MathF.Sin(angle), MathF.Cos(angle), p.Y * MathF.Sin(angle));
            return new(center + unit * radius, (unit / radius).Normalized());
        }
        for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < _circle.Length; i++)
            {
                Vector2 a = _circle[i], b = _circle[(i + 1) % _circle.Length];
                Triangle(Point(ring, a), Point(ring, b), Point(ring + 1, b), color);
                Triangle(Point(ring, a), Point(ring + 1, b), Point(ring + 1, a), color);
            }
    }

    private void Patch(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 color)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).Normalized();
        if (normal.Z < 0) normal = -normal;
        Triangle(new(a, normal), new(b, normal), new(c, normal), color);
        Triangle(new(a, normal), new(c, normal), new(d, normal), color);
    }

    private void Triangle(SurfacePoint a, SurfacePoint b, SurfacePoint c, Vector3 color)
    {
        Vector3 geometricNormal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
        if (geometricNormal.LengthSquared < 1e-14f) return;
        if (Vector3.Dot(geometricNormal, a.Normal + b.Normal + c.Normal) < 0) (b, c) = (c, b);
        if (FloatCount + 27 > _data.Length) Array.Resize(ref _data, _data.Length * 2);
        Emit(a, color); Emit(b, color); Emit(c, color);
    }

    private void Emit(SurfacePoint p, Vector3 color)
    {
        Vector3 local = p.Position * _height;
        Vector3 world = _origin + Rotate(local);
        Vector3 normal = Rotate(p.Normal);
        _data[FloatCount++] = world.X; _data[FloatCount++] = world.Y; _data[FloatCount++] = world.Z;
        _data[FloatCount++] = color.X; _data[FloatCount++] = color.Y; _data[FloatCount++] = color.Z;
        _data[FloatCount++] = normal.X; _data[FloatCount++] = normal.Y; _data[FloatCount++] = normal.Z;
    }

    private Vector3 Rotate(Vector3 p) => new(p.X * _cos + p.Z * _sin, p.Y, -p.X * _sin + p.Z * _cos);
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    private static Vector2[] MakeCircle(int segments)
    {
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * MathHelper.TwoPi / segments;
            points[i] = new(MathF.Cos(angle), MathF.Sin(angle));
        }
        return points;
    }

    private static Vector3[] MakeSphere(int segments, int rings)
    {
        var points = new Vector3[segments * (rings - 1) * 6];
        int index = 0;
        Vector3 Point(int ring, int segment)
        {
            float theta = ring * MathF.PI / rings, phi = segment * MathHelper.TwoPi / segments;
            return new(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
        }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < segments; j++)
            {
                Vector3 a = Point(i, j), b = Point(i, j + 1), c = Point(i + 1, j + 1), d = Point(i + 1, j);
                if (i > 0) { points[index++] = a; points[index++] = b; points[index++] = c; }
                if (i < rings - 1) { points[index++] = a; points[index++] = c; points[index++] = d; }
            }
        return points;
    }
}
