using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

internal static class SceneGeometry
{
    internal static void Quad(List<float> vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 color)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (normal.LengthSquared < 1e-12f) return;
        normal.Normalize();
        Vertex(vertices, a, color, normal); Vertex(vertices, b, color, normal); Vertex(vertices, c, color, normal);
        Vertex(vertices, a, color, normal); Vertex(vertices, c, color, normal); Vertex(vertices, d, color, normal);
    }

    internal static void Ground(List<float> v, float x, float z, float width, float depth, float y, Vector3 color) =>
        Quad(v, new(x, y, z), new(x, y, z + depth), new(x + width, y, z + depth), new(x + width, y, z), color);

    internal static void Box(List<float> v, Vector3 min, Vector3 size, Vector3 color)
    {
        Vector3 max = min + size;
        float x = min.X, y = min.Y, z = min.Z, X = max.X, Y = max.Y, Z = max.Z;
        Quad(v, new(x,y,Z), new(X,y,Z), new(X,Y,Z), new(x,Y,Z), color);
        Quad(v, new(X,y,z), new(x,y,z), new(x,Y,z), new(X,Y,z), color);
        Quad(v, new(X,y,Z), new(X,y,z), new(X,Y,z), new(X,Y,Z), color);
        Quad(v, new(x,y,z), new(x,y,Z), new(x,Y,Z), new(x,Y,z), color);
        Ground(v, x, z, size.X, size.Z, Y, color);
        Quad(v, new(x,y,z), new(X,y,z), new(X,y,Z), new(x,y,Z), color);
    }

    internal static void Foliage(List<float> v, Vector3 center, Vector3 radius, Vector3 color)
    {
        const int segments = 8, rings = 5;
        Vector3 Point(int ring, int segment)
        {
            float a = ring * MathF.PI / rings, b = segment * MathHelper.TwoPi / segments;
            return center + radius * new Vector3(MathF.Sin(a) * MathF.Cos(b), MathF.Cos(a), MathF.Sin(a) * MathF.Sin(b));
        }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < segments; j++)
            {
                Vector3 a = Point(i,j), b = Point(i+1,j), c = Point(i+1,j+1), d = Point(i,j+1);
                Vector3 shade = color * (0.93f + 0.07f * ((i + j) % 3));
                if (i == 0) Triangle(v, a, c, b, shade);
                else if (i == rings - 1) Triangle(v, a, d, c, shade);
                else Quad(v, a, d, c, b, shade);
            }
    }

    private static void Triangle(List<float> v, Vector3 a, Vector3 b, Vector3 c, Vector3 color)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).Normalized();
        Vertex(v, a, color, normal); Vertex(v, b, color, normal); Vertex(v, c, color, normal);
    }

    private static void Vertex(List<float> v, Vector3 p, Vector3 color, Vector3 n)
    {
        v.Add(p.X); v.Add(p.Y); v.Add(p.Z);
        v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
        v.Add(n.X); v.Add(n.Y); v.Add(n.Z);
    }
}
