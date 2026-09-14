using System;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal readonly record struct SceneRange(int First, int Count, Vector3 Center, float Radius, int Material = 0)
{
    internal bool Visible(in Matrix4 view, in Matrix4 projection, float far = 190)
    {
        Vector3 p = Vector3.TransformPosition(Center, view);
        float depth = -p.Z;
        return depth + Radius > 0 && depth - Radius < far &&
            Math.Abs(p.X) - Radius <= (depth + Radius) / Math.Abs(projection.M11) &&
            Math.Abs(p.Y) - Radius <= (depth + Radius) / Math.Abs(projection.M22);
    }

    internal bool Near(Vector3 focus, float distance) =>
        Vector2.DistanceSquared(Center.Xz, focus.Xz) < (distance + Radius) * (distance + Radius);
}
