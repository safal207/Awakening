using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public sealed partial class CharacterMesh
{
    private readonly record struct Section(float Level, float Width, float Depth, float Forward = 0);
    private static readonly Vector2[] HeroCircle = MakeCircle(16);
    private static readonly Vector2[] HeroFarCircle = MakeCircle(4);
    private static readonly Vector2[] HeroRoundCircle = MakeRoundedCircle(16);
    private static readonly Vector2[] LowRoundCircle = MakeRoundedCircle(6);
    private static readonly Vector2[] FarRoundCircle = MakeRoundedCircle(4);
    private readonly SurfacePoint[] _profilePoints = new SurfacePoint[16 * 12];
    private static readonly Section[] Chest = {
        new(.546f,.094f,.062f), new(.580f,.090f,.063f), new(.635f,.094f,.069f),
        new(.705f,.110f,.076f), new(.755f,.116f,.073f), new(.789f,.110f,.061f),
        new(.815f,.083f,.043f), new(.839f,.031f,.028f)
    };
    private static readonly Section[] Pelvis = {
        new(.482f,.070f,.043f), new(.508f,.094f,.061f), new(.534f,.097f,.065f), new(.551f,.092f,.061f)
    };
    private static readonly Section[] WorkBelt = {
        new(.545f,.096f,.064f), new(.555f,.095f,.064f)
    };
    private static readonly Section[] Head = {
        new(.855f,.019f,.026f,.010f), new(.868f,.034f,.035f,.007f),
        new(.887f,.045f,.040f,.003f), new(.911f,.052f,.044f),
        new(.934f,.051f,.044f), new(.957f,.049f,.043f),
        new(.978f,.040f,.036f), new(.989f,.024f,.023f), new(.994f,.005f,.007f)
    };
    private static readonly Section[] Trousers = {
        new(0,.046f,.048f), new(.1f,.049f,.052f), new(.33f,.041f,.044f), new(.5f,.034f,.034f),
        new(.62f,.037f,.038f), new(.85f,.030f,.030f), new(1,.027f,.026f)
    };
    private static readonly Section[] Sleeve = {
        new(0,.007f,.011f), new(.15f,.035f,.038f), new(.48f,.034f,.036f), new(1,.027f,.029f)
    };
    private static readonly Section[] Forearm = {
        new(0,.025f,.025f), new(.22f,.026f,.027f), new(.7f,.020f,.021f), new(1,.016f,.018f)
    };
    private static readonly Section[] Palm = {
        new(0,.016f,.012f), new(.013f,.020f,.013f), new(.033f,.018f,.011f), new(.042f,.014f,.010f)
    };
    private static readonly Section[] Finger = {
        new(0,.0048f,.0045f), new(.4f,.0046f,.0045f,.002f),
        new(.8f,.0038f,.0038f,.004f), new(1,.0015f,.002f,.006f)
    };
    private static readonly Section[] Nose = {
        new(.907f,.004f,.003f,.046f), new(.913f,.007f,.006f,.051f),
        new(.924f,.004f,.004f,.048f), new(.941f,.003f,.002f,.043f)
    };
    private static readonly Section[] Sole = {
        new(0,.034f,.062f,.021f), new(.007f,.036f,.064f,.021f), new(.014f,.035f,.062f,.021f)
    };
    private static readonly Section[] Boot = {
        new(.014f,.035f,.062f,.021f), new(.026f,.033f,.058f,.022f),
        new(.038f,.028f,.044f,.009f), new(.055f,.025f,.032f), new(.073f,.023f,.027f)
    };

    private void AppendHero(NpcCharacter npc, float time, float blend)
    {
        _circle = _detail == CharacterDetail.Full ? HeroCircle :
            _detail == CharacterDetail.Reduced ? LowCircle : HeroFarCircle;
        CharacterPose pose = HeroPose.Create(npc.AnimPhase, blend);
        float bodyY = HeroPose.BodyOffset(blend);
        float breath = MathF.Sin(time * 1.8f) * .0012f * (1 - blend);
        float upperY = bodyY + breath;
        Vector3 skin = npc.HeadColor, shirt = HeroStyle.ShirtBlue, pants = npc.PantsColor;
        HeroLeg(pose.LeftLeg, pants);
        HeroLeg(pose.RightLeg, pants);
        Loft(Pelvis, new(0, bodyY, 0), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, pants);
        Loft(Chest, new(0, upperY, 0), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, shirt, rounded: true);
        HeroArm(pose.LeftArm, shirt, skin, breath);
        HeroArm(pose.RightArm, shirt, skin, breath);
        Tube(new(0, .815f + upperY, 0), new(0, .877f + upperY, 0), .025f, .027f, skin);
        Loft(Head, new(0, upperY, 0), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, skin, rounded: true);
        HeroHair(upperY, npc.HairColor);
        if (_detail != CharacterDetail.Full) return;
        HeroClothes(upperY, shirt);
        HeroFace(upperY, skin, npc.HairColor, time);
    }

    // Shared rings and smooth profile normals avoid stacked cylinders and exposed end caps.
    private void Loft(Section[] sections, Vector3 origin, Vector3 axis, Vector3 u, Vector3 v,
        float length, Vector3 color, bool rounded = false, LimbPose? bend = null)
    {
        Vector2[] contour = !rounded ? _circle : _detail == CharacterDetail.Full ? HeroRoundCircle :
            _detail == CharacterDetail.Reduced ? LowRoundCircle : FarRoundCircle;
        int stride = contour.Length;
        for (int ring = 0; ring < sections.Length; ring++)
        {
            Section s = sections[ring];
            Vector3 center = origin + axis * (s.Level * length), right = u, forward = v;
            if (bend is LimbPose limb)
            {
                Vector3 first = (limb.Joint - limb.Root).Normalized(), second = (limb.Tip - limb.Joint).Normalized();
                center = s.Level <= .5f ? Vector3.Lerp(limb.Root, limb.Joint, s.Level * 2) :
                    Vector3.Lerp(limb.Joint, limb.Tip, (s.Level - .5f) * 2);
                Vector3 direction = s.Level < .5f ? first : s.Level > .5f ? second : (first + second).Normalized();
                right = (Vector3.UnitX - direction * direction.X).Normalized();
                forward = Vector3.Cross(right, direction);
            }
            for (int j = 0; j < stride; j++)
            {
                Vector2 p = contour[j];
                _profilePoints[ring * stride + j] = new(center + right * (p.X * s.Width) + forward * (p.Y * s.Depth + s.Forward), Vector3.Zero);
            }
        }
        SurfacePoint Point(int ring, int segment) => _profilePoints[ring * stride + (segment + stride) % stride];
        for (int ring = 0; ring < sections.Length; ring++)
            for (int j = 0; j < stride; j++)
            {
                Vector3 tangent = Point(ring, j + 1).Position - Point(ring, j - 1).Position;
                Vector3 vertical = Point(Math.Min(ring + 1, sections.Length - 1), j).Position -
                    Point(Math.Max(ring - 1, 0), j).Position;
                _profilePoints[ring * stride + j] = new(Point(ring, j).Position, Vector3.Cross(vertical, tangent).Normalized());
            }
        for (int ring = 0; ring < sections.Length - 1; ring++)
            for (int j = 0; j < stride; j++)
            {
                SurfacePoint a = Point(ring, j), b = Point(ring, j + 1), c = Point(ring + 1, j + 1), d = Point(ring + 1, j);
                Triangle(a, d, c, color); Triangle(a, c, b, color);
            }
        for (int ring = 0; ring < sections.Length; ring += sections.Length - 1)
        {
            Vector3 center = Vector3.Zero;
            for (int j = 0; j < stride; j++) center += Point(ring, j).Position;
            center /= stride;
            Vector3 normal = Vector3.Cross(Point(ring, 1).Position - Point(ring, 0).Position,
                Point(ring, 2).Position - Point(ring, 0).Position).Normalized();
            if (ring > 0) normal = -normal;
            for (int j = 0; j < stride; j++)
            {
                Triangle(new(center, normal), new(Point(ring, j).Position, normal), new(Point(ring, j + 1).Position, normal), color);
            }
        }
    }

    private void LimbLoft(Section[] sections, Vector3 start, Vector3 end, Vector3 color)
    {
        Vector3 delta = end - start, axis = delta.Normalized();
        Vector3 u = (Vector3.UnitX - axis * axis.X).Normalized();
        Loft(sections, start, axis, u, Vector3.Cross(u, axis), delta.Length, color);
    }

    private void HeroLeg(LimbPose leg, Vector3 pants)
    {
        Loft(Trousers, Vector3.Zero, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, pants, bend: leg);
        Vector3 foot = leg.Tip - Vector3.UnitY * HeroPose.AnkleHeight;
        Loft(Sole, foot, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, HeroStyle.Shoe, rounded: true);
        Loft(Boot, foot, Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, HeroStyle.WorkLeather, rounded: true);
        if (_detail != CharacterDetail.Full) return;
        Vector2[] circle = _circle;
        _circle = LowCircle;
        for (int i = 0; i < 3; i++)
        {
            float y = .034f + i * .006f, z = .049f - i * .009f;
            Tube(foot + new Vector3(-.016f, y, z), foot + new Vector3(.016f, y + .002f, z), .0014f, .0014f, HeroStyle.Stitch);
        }
        _circle = circle;
    }

    private void HeroArm(LimbPose source, Vector3 shirt, Vector3 skin, float breath)
    {
        Vector3 offset = Vector3.UnitY * breath;
        Vector3 root = source.Root + offset, elbow = source.Joint + offset, wrist = source.Tip + offset;
        Vector3 upper = (elbow - root).Normalized(), lower = (wrist - elbow).Normalized();
        Vector3 cuff = Vector3.Lerp(root, elbow, .86f);
        float side = Math.Sign(root.X);
        LimbLoft(Sleeve, root + new Vector3(-side * .020f, .017f, 0), cuff, shirt);
        Tube(cuff - upper * .012f, cuff + upper * .005f, .029f, .029f, shirt * 1.13f);
        Tube(cuff, elbow + lower * .012f, .025f, .025f, skin);
        LimbLoft(Forearm, elbow, wrist, skin);
        Vector3 u = (Vector3.UnitX - lower * lower.X).Normalized(), v = Vector3.Cross(u, lower);
        Loft(Palm, wrist, lower, u, v, 1, skin, rounded: true);
        if (_detail != CharacterDetail.Full) return;
        Vector2[] circle = _circle;
        _circle = LowCircle;
        for (int i = 0; i < 4; i++)
        {
            Vector3 finger = wrist + lower * .034f + u * ((i - 1.5f) * .0085f);
            float length = i == 0 || i == 3 ? .023f : .028f;
            Loft(Finger, finger, lower, u, v, length, skin * .98f);
        }
        Vector3 thumb = wrist + lower * .014f - u * (side * .017f);
        Tube(thumb, thumb + lower * .022f - u * (side * .009f) + v * .008f, .007f, .004f, skin);
        if (side < 0)
        {
            Tube(wrist - lower * .023f, wrist - lower * .011f, .021f, .021f, HeroStyle.WorkLeather);
            Vector3 watch = wrist - lower * .017f + Vector3.UnitZ * .021f;
            Patch(watch + new Vector3(-.012f, -.009f, 0), watch + new Vector3(.012f, -.009f, 0),
                watch + new Vector3(.012f, .009f, 0), watch + new Vector3(-.012f, .009f, 0), HeroStyle.Metal);
            Patch(watch + new Vector3(-.008f, -.005f, .001f), watch + new Vector3(.008f, -.005f, .001f),
                watch + new Vector3(.008f, .005f, .001f), watch + new Vector3(-.008f, .005f, .001f), HeroStyle.Eye);
        }
        _circle = circle;
    }

    private static float Front(Section[] sections, float x, float y, float roundness = .82f)
    {
        int i = 0;
        while (i < sections.Length - 2 && sections[i + 1].Level < y) i++;
        Section a = sections[i], b = sections[i + 1];
        float t = Math.Clamp((y - a.Level) / (b.Level - a.Level), 0, 1);
        float width = a.Width + (b.Width - a.Width) * t, depth = a.Depth + (b.Depth - a.Depth) * t;
        float cos = MathF.Pow(Math.Clamp(Math.Abs(x) / width, 0, 1), 1 / roundness);
        return a.Forward + (b.Forward - a.Forward) * t + MathF.Pow(Math.Max(0, 1 - cos * cos), roundness * .5f) * depth;
    }

    private void ClothQuad(float x1, float y1, float x2, float y2, float offset, Vector3 color,
        bool back = false, float relief = .004f)
    {
        float side = back ? -1 : 1;
        SurfacePoint P(float x, float y) => new(new(x, y + offset, side * (Front(Chest, x, y) + relief)), Vector3.UnitZ * side);
        int columns = x2 - x1 > .1f ? 8 : x2 - x1 > .02f ? 3 : 1, rows = y2 - y1 > .03f ? 3 : 1;
        for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++)
            {
                float left = x1 + (x2 - x1) * col / columns, right = x1 + (x2 - x1) * (col + 1) / columns;
                float low = y1 + (y2 - y1) * row / rows, high = y1 + (y2 - y1) * (row + 1) / rows;
                Triangle(P(left, low), P(right, low), P(right, high), color);
                Triangle(P(left, low), P(right, high), P(left, high), color);
            }
    }

    private void HeroClothes(float offset, Vector3 shirt)
    {
        for (int i = 0; i < 6; i++)
            ClothQuad(-.006f, .568f + i * .035f, .006f, .603f + i * .035f, offset, shirt * .92f);
        for (int i = 0; i < 5; i++)
        {
            float y = .586f + i * .04f;
            Ellipsoid(new(0, y + offset, Front(Chest, 0, y) + .004f), new(.0025f, .0025f, .0016f), HeroStyle.Metal, small: true);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 P(float x, float y) => new(x * side, y + offset, Front(Chest, x, y) + .0025f);
            Patch(P(.007f, .821f), P(.029f, .806f), P(.044f, .822f), P(.030f, .839f), shirt * 1.12f);
        }
        ClothQuad(-.079f, .704f, -.032f, .748f, offset, shirt * .95f);
        ClothQuad(-.079f, .739f, -.032f, .749f, offset, shirt * .84f, relief: .0055f);
        ClothQuad(-.066f, .725f, -.042f, .732f, offset, HeroStyle.Stitch, relief: .0055f);
        ClothQuad(.034f, .718f, .079f, .752f, offset, shirt * .96f);
        ClothQuad(.034f, .743f, .079f, .752f, offset, shirt * .85f, relief: .0055f);
        ClothQuad(.050f, .747f, .054f, .771f, offset, HeroStyle.Metal, relief: .007f);
        ClothQuad(-.092f, .752f, .092f, .754f, offset, shirt * .86f, back: true);
        Loft(WorkBelt, new(0, offset, 0), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1,
            HeroStyle.WorkLeather, rounded: true);
        Patch(new(-.013f, .544f + offset, .065f), new(.013f, .544f + offset, .065f),
            new(.013f, .555f + offset, .065f), new(-.013f, .555f + offset, .065f), HeroStyle.Metal);
    }

    private void HeroFace(float offset, Vector3 skin, Vector3 hair, float time)
    {
        Vector2[] circle = _circle;
        _circle = LowCircle;
        for (int side = -1; side <= 1; side += 2)
        {
            Ellipsoid(new(side * .051f, .923f + offset, -.002f), new(.007f, .016f, .009f), skin * .96f);
            float x = side * .020f, y = .935f + offset, z = Front(Head, x, .935f);
            float blinkTime = (time + 1.7f) % 5.3f;
            float openness = blinkTime < .16f ? Math.Max(.12f, Math.Abs(blinkTime - .08f) / .08f) : 1;
            Ellipsoid(new(x, y, z + .0005f), new(.0084f, .0036f * openness, .0028f), new(.78f, .80f, .76f), small: true);
            Ellipsoid(new(x, y, z + .0028f), new(.0033f, .0032f * openness, .0010f), HeroStyle.Iris, small: true);
            Ellipsoid(new(x, y, z + .0035f), new(.0016f, .0024f * openness, .0006f), HeroStyle.Eye, small: true);
            Tube(new(x - .009f, y + .0026f * openness, z + .002f), new(x + .009f, y + .0029f * openness, z + .002f),
                .0009f, .0009f, skin * .57f);
            Tube(new(x - side * .010f, y + .011f, z - .001f), new(x + side * .010f, y + .009f, z - .003f), .0016f, .001f, hair);
        }
        Loft(Nose, new(0, offset, 0), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 1, skin);
        float mouthZ = Front(Head, 0, .891f);
        Tube(new(-.012f, .892f + offset, mouthZ + .001f), new(0, .891f + offset, mouthZ + .002f), .001f, .0012f, skin * .52f);
        Tube(new(0, .891f + offset, mouthZ + .002f), new(.012f, .8925f + offset, mouthZ + .001f), .0012f, .0008f, skin * .52f);
        _circle = circle;
    }

    private static Vector2[] MakeRoundedCircle(int segments)
    {
        Vector2[] circle = MakeCircle(segments);
        for (int i = 0; i < circle.Length; i++)
            circle[i] = new(MathF.CopySign(MathF.Pow(Math.Abs(circle[i].X), .82f), circle[i].X),
                MathF.CopySign(MathF.Pow(Math.Abs(circle[i].Y), .82f), circle[i].Y));
        return circle;
    }

    private void HeroHair(float offset, Vector3 color)
    {
        int rings = _detail == CharacterDetail.Full ? 6 : 3;
        SurfacePoint Point(int ring, Vector2 p)
        {
            float fringe = .17f * Math.Max(0, p.Y) * Math.Max(0, -p.X);
            float t = ring / (float)rings, angle = (1.83f - .50f * p.Y - .08f * p.X + fringe) * t;
            float x = MathF.CopySign(MathF.Pow(Math.Abs(p.X), .82f), p.X);
            float z = MathF.CopySign(MathF.Pow(Math.Abs(p.Y), .82f), p.Y);
            Vector3 unit = new(x * MathF.Sin(angle), MathF.Cos(angle), z * MathF.Sin(angle));
            Vector3 radius = new(.057f, .055f, .052f);
            Vector3 position = new Vector3(-.005f * (1 - t), .945f + offset, 0) + unit * radius;
            return new(position, (unit / radius).Normalized());
        }
        for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < _circle.Length; i++)
            {
                Vector2 a = _circle[i], b = _circle[(i + 1) % _circle.Length];
                Vector3 shade = color * (.94f + .09f * (a.X * .5f + .5f));
                Triangle(Point(ring, a), Point(ring, b), Point(ring + 1, b), shade);
                Triangle(Point(ring, a), Point(ring + 1, b), Point(ring + 1, a), shade);
            }
        if (_detail != CharacterDetail.Full) return;
        Vector2[] circle = _circle;
        _circle = LowCircle;
        // A restrained side part follows the cap rather than floating above the forehead.
        Vector2 part = new(.36f, .93295f);
        for (int ring = 1; ring < rings; ring++)
        {
            SurfacePoint a = Point(ring, part), b = Point(ring + 1, part);
            Tube(a.Position + a.Normal * .0008f, b.Position + b.Normal * .0008f, .0007f, .0007f, color * .66f);
        }
        _circle = circle;
    }
}
